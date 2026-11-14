# P3CardArt 1.0.1 · 0.113 卡图兼容修正

用户反馈独立卡面 Mod 在 0.113 未显示，日志显示 DLL、PCK 与 94 张图均已加载，没有初始化失败或解码错误。

0.113 的完整卡牌查看界面增加 BigPortrait 路径，旧版只补丁 Portrait。小型 getter 还可能被 JIT 内联，导致已编译的调用不经过后加的补丁；该内联因素未在游戏内单独证明。1.0.1 同时覆盖 Portrait 和可用的 BigPortrait，并在 NCard.UpdatePortrait 后直接替换相应 TextureRect，保持卡框与文字不变，刷新隐藏牌的模糊参数。

构建引用搜索改为优先指定 GameDir 的 HintPath，避免被项目下旧 DLL 副本抢先解析。对两套实际游戏程序集运行 Harmony 安装检查：0.111 的 Portrait、NCard.UpdatePortrait 与 0.113 的 Portrait、BigPortrait、NCard.UpdatePortrait 均成功挂上补丁，且字段存在。运行检查使用 .NET 9；.NET 10 不受游戏自带 Harmony 支持。两版 Release 构建均 0 警告、0 错误。

已备份并安装到桌面 v0.111 与 Steam v0.113，ZIP 内三份文件及当前安装哈希均与构建记录一致。PCK 沿用原包，SHA-256 为 `562351a67953239c02570a9c5c23eaf8bb48542a75dc8dca5e71c795ad612f95`，94 张图片完全未改动。ZIP 有桌面 P3RMOD 副本。

用户后续确认“改动成功了”，0.113 卡面替换已获得用户实机确认；随后报告的伤害数字、音效与商店问题属于主 Mod，另见 [0.15.33 报告](game113_0.15.33.md)。

构建、补丁安装检查及安装/ZIP 清单见 `card_art_1.0.1_compat/`。
