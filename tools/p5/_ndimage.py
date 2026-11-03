"""scipy.ndimage 里姿势检查脚本用到的三个函数的纯 numpy 版（Codex 那边没有 scipy、规则又不许装东西时用）。
只实现 binary_closing / label / sum 这三个、只按脚本里的用法（3×3 结构元素、4 邻接也够用，这里用 8 邻接和 scipy 默认一致的十字结构）。"""
import numpy as np


def _dilate(m):
    out = m.copy()
    out[1:, :] |= m[:-1, :]
    out[:-1, :] |= m[1:, :]
    out[:, 1:] |= m[:, :-1]
    out[:, :-1] |= m[:, 1:]
    return out


def _erode(m):
    out = m.copy()
    out[1:, :] &= m[:-1, :]
    out[:-1, :] &= m[1:, :]
    out[:, 1:] &= m[:, :-1]
    out[:, :-1] &= m[:, 1:]
    out[0, :] = out[-1, :] = False
    out[:, 0] = out[:, -1] = False
    return out


def binary_closing(m, iterations=1):
    """和 scipy 一样不补边：膨胀 n 次再腐蚀 n 次，画面外一律当空（border_value=0）。"""
    p = np.asarray(m, bool)
    for _ in range(iterations):
        p = _dilate(p)
    for _ in range(iterations):
        p = _erode(p)
    return p


def label(m):
    """十字邻接（scipy 默认结构）连通域：两遍扫描 + 并查集，返回 (labels, count)。"""
    m = np.asarray(m, bool)
    h, w = m.shape
    lab = np.zeros((h, w), np.int32)
    parent = [0]

    def find(a):
        while parent[a] != a:
            parent[a] = parent[parent[a]]
            a = parent[a]
        return a

    nxt = 1
    for y in range(h):
        row = m[y]
        if not row.any():
            continue
        up = lab[y - 1] if y else None
        xs = np.nonzero(row)[0]
        for x in xs:
            l_left = lab[y, x - 1] if x and row[x - 1] else 0
            l_up = up[x] if up is not None and up[x] else 0
            if l_left and l_up:
                a, b = find(l_left), find(l_up)
                lab[y, x] = min(a, b)
                if a != b:
                    parent[max(a, b)] = min(a, b)
            elif l_left or l_up:
                lab[y, x] = l_left or l_up
            else:
                parent.append(nxt)
                lab[y, x] = nxt
                nxt += 1
    roots = np.array([find(i) for i in range(nxt)], np.int32)
    uniq = {r: i for i, r in enumerate(sorted(set(roots[1:].tolist())), start=1)}
    remap = np.zeros(nxt, np.int32)
    for i in range(1, nxt):
        remap[i] = uniq[roots[i]]
    return remap[lab], len(uniq)


def sum(input, labels, index):  # noqa: A001 - 和 scipy 同名
    idx = list(index)
    totals = np.bincount(labels.ravel(), weights=np.asarray(input, float).ravel(), minlength=(max(idx) + 1) if idx else 1)
    return np.array([totals[i] for i in idx])
