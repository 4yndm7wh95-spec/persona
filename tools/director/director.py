"""P3 演示录制导演脚本：按镜头清单驱动游戏（P3TestMod 导演接口）+ OBS（WebSocket）逐段录制。

用法（Windows，游戏已开、装了 P3Makoto + P3TestMod、OBS 已开）：
  python director.py ping                         # 检查游戏接口和 OBS 是否都连得上
  python director.py state                        # 当前画面 / 手牌 / 敌人
  python director.py cards [regent|colorless|all] # 列出卡牌 id
  python director.py call play '{"card":"SovereignBlade"}'   # 手动调一个接口（不录制）
  python director.py run shots.json               # 按清单逐段录制
  python director.py run shots.json --only 03_sovereign_blade --no-obs

每段录完，文件移到 <录制目录>/<run 时间>/<分类>/<序号>_<镜头名>.<扩展名>，
同目录写 manifest.json：每段的文件、时长、每一步相对录制开始的时间点（剪辑时对齐用）。
依赖：pip install obsws-python requests
"""
from __future__ import annotations

import argparse
import json
import os
import shutil
import sys
import time
from pathlib import Path

import requests

BRIDGE = os.environ.get("P3_BRIDGE_URL", "http://127.0.0.1:47021")
OBS_CONFIG = Path(os.environ.get("P3_OBS_CONFIG", Path.home() / ".p3record" / "obs.json"))


class BridgeError(RuntimeError):
    pass


def call(endpoint: str, args: dict | None = None, timeout: float = 120) -> dict:
    """调用游戏导演接口（POST JSON）。"""
    url = BRIDGE + "/" + endpoint.lstrip("/")
    try:
        r = requests.post(url, json=args or {}, timeout=timeout)
    except requests.ConnectionError as e:
        raise BridgeError(f"连不上游戏导演接口 {BRIDGE}（游戏开了吗？P3TestMod 装了吗？）") from e
    data = r.json()
    if not data.get("ok", False):
        raise BridgeError(f"{endpoint} 失败：{data.get('error')}")
    return data


class Obs:
    def __init__(self) -> None:
        import obsws_python as obs  # 延迟导入：--no-obs 时不需要

        cfg = json.loads(OBS_CONFIG.read_text(encoding="utf-8"))
        self.cfg = cfg
        self.client = obs.ReqClient(host=cfg.get("host", "127.0.0.1"), port=cfg.get("port", 4455), password=cfg.get("password", ""), timeout=10)

    @property
    def record_dir(self) -> Path:
        return Path(self.cfg.get("record_dir") or self.client.get_record_directory().record_directory)

    def start(self) -> None:
        if self.client.get_record_status().output_active:
            self.client.stop_record()
            time.sleep(1.0)
        self.client.start_record()
        for _ in range(50):  # 等到真的开始写文件
            if self.client.get_record_status().output_active:
                return
            time.sleep(0.1)
        raise RuntimeError("OBS 没有开始录制")

    def stop(self) -> Path:
        resp = self.client.stop_record()
        return Path(resp.output_path)


def move_when_released(src: Path, dst: Path, wait_s: float = 20) -> Path:
    """OBS 停止后还要收尾写文件（hybrid mp4 / remux），能移动了再移动。"""
    dst.parent.mkdir(parents=True, exist_ok=True)
    end = time.time() + wait_s
    while True:
        try:
            shutil.move(str(src), str(dst))
            return dst
        except (PermissionError, OSError):
            if time.time() > end:
                print(f"  ! 文件还被占用，保留原位置：{src}")
                return src
            time.sleep(0.5)


def run_step(step: dict) -> None:
    step = dict(step)
    do = step.pop("do")
    if do == "sleep":
        time.sleep(step.get("ms", 0) / 1000)
    elif do == "note":
        pass
    else:
        call(do, step)


def run_shots(path: Path, only: list[str] | None, use_obs: bool) -> Path | None:
    plan = json.loads(path.read_text(encoding="utf-8"))
    defaults = plan.get("defaults", {})
    obs = Obs() if use_obs else None
    out_root = (obs.record_dir if obs else Path("."))/ time.strftime("run_%Y%m%d_%H%M%S")
    manifest = {"plan": str(path), "started": time.strftime("%Y-%m-%d %H:%M:%S"), "shots": []}
    call("ping")
    call("ui", {"test_button": False})
    for i, shot in enumerate(plan["shots"], 1):
        name = shot["name"]
        if only and name not in only:
            continue
        print(f"[{i:02d}] {name} — {shot.get('desc', '')}")
        need = shot.get("requires")
        if need:
            screen = call("state")["screen"]
            if screen != need:
                print(f"  ! 需要画面 {need}，当前是 {screen}，跳过")
                manifest["shots"].append({"name": name, "skipped": f"screen={screen}"})
                continue
        for step in shot.get("setup", []):
            run_step(step)
        if need == "combat":
            call("wait", {"until": "ready", "timeout_ms": 20000})
        call("mouse/park")
        pre = shot.get("pre_roll_ms", defaults.get("pre_roll_ms", 800))
        post = shot.get("post_roll_ms", defaults.get("post_roll_ms", 1500))
        marks = []
        if obs:
            obs.start()
        t0 = time.time()
        time.sleep(pre / 1000)
        error = None
        try:
            for step in shot["steps"]:
                marks.append({"t": round(time.time() - t0, 2), "step": step})
                run_step(step)
        except BridgeError as e:
            error = str(e)
            print(f"  ! {error}")
        time.sleep(post / 1000)
        duration = round(time.time() - t0, 2)
        entry = {"name": name, "category": shot.get("category", "misc"), "desc": shot.get("desc", ""), "duration": duration, "marks": marks}
        if error:
            entry["error"] = error
        if obs:
            raw = obs.stop()
            dst = out_root / entry["category"] / f"{i:02d}_{name}{raw.suffix}"
            entry["file"] = str(move_when_released(raw, dst))
            print(f"  → {entry['file']}  ({duration}s)")
        manifest["shots"].append(entry)
        time.sleep(shot.get("gap_ms", defaults.get("gap_ms", 600)) / 1000)
    call("ui", {"test_button": True})
    if obs:
        out_root.mkdir(parents=True, exist_ok=True)
        (out_root / "manifest.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8")
        print(f"完成：{out_root}")
        return out_root
    print(json.dumps(manifest, ensure_ascii=False, indent=2))
    return None


def main() -> None:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = ap.add_subparsers(dest="cmd", required=True)
    sub.add_parser("ping")
    sub.add_parser("state")
    c = sub.add_parser("cards")
    c.add_argument("pool", nargs="?", default="regent")
    c.add_argument("q", nargs="?", default="")
    k = sub.add_parser("call")
    k.add_argument("endpoint")
    k.add_argument("json", nargs="?", default="{}")
    r = sub.add_parser("run")
    r.add_argument("plan", type=Path)
    r.add_argument("--only", nargs="*")
    r.add_argument("--no-obs", action="store_true")
    a = ap.parse_args()

    if a.cmd == "ping":
        print("game:", call("ping"))
        o = Obs()
        print("obs:", o.client.get_version().obs_version, "record_dir:", o.record_dir)
    elif a.cmd == "state":
        print(json.dumps(call("state"), ensure_ascii=False, indent=2))
    elif a.cmd == "cards":
        for card in call("cards", {"pool": a.pool, "q": a.q})["cards"]:
            print(f"{card['cls']:<24} {card['id']:<28} {card['title']}  [{card['target']}]")
    elif a.cmd == "call":
        print(json.dumps(call(a.endpoint, json.loads(a.json)), ensure_ascii=False, indent=2))
    elif a.cmd == "run":
        run_shots(a.plan, a.only, not a.no_obs)


if __name__ == "__main__":
    try:
        main()
    except BridgeError as e:
        print("错误：", e, file=sys.stderr)
        sys.exit(1)
