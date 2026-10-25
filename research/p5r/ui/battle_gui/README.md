# 原版战斗 GUI

已按 index.csv 检查 BATTLE/GUI 全部 125 项，直接 DDS 和 GFS/SPD 内有效嵌入 DDS 共导出 72 张纹理。几何、脚本及无 DDS 的文件不冒充可 PNG 化资源，详见 container_audit.csv。

四份原有 SPD 坐标导出连同其 590 张切图已复制到 sprites/，原始 JSON 未修改。sprite_comments.csv 汇总原注释与坐标。

确认可用：DAMAGE_DATA.SPD 的数字 0–9 为索引79–88；WEAK为94、CRITICAL为95、TECHNICAL为125；技能名框为120（原注释 スキル名土台），121–124是揭开/撕纸片段。均在 sprites/DAMAGE_DATA.SPD_unpacked__a87542a9d6/，文件名为补足五位的索引。
P5_BATTLE_PARTYPANEL 的97–160包含原注释标记的炎上、冻结、忘却、感电、眩晕、睡眠、混乱、恐惧等状态异常图标及底图。

未可靠定位独立的 1 MORE、MISS 英文字样和增益/减益箭头；不把中文文字或空白占位图改名冒充。技能名 EIHA/SUKUKAJA 不是当前已定位的独立字样贴图，需后续确认字库/动态文字渲染。

联系表用深灰底辅助观察，原始 PNG 保留原 alpha，不把原版不透明区域自动抠掉。
