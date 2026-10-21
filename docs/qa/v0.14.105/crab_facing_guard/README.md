# 螃蟹房识别修正

- 上一包确实已安装：两套游戏 DLL 与 7f6ac240 快照构建一致，SHA256 3D09F72537DDEE5B90D4AB0FF6CF0C08B9F8E5C56D190CD25B9D9A048A09D950。
- 未生效根因：NCombatRoom.Background 是 NCombatBackground；NKaiserCrabBossBackground 继承 Node2D，是内部控制器，类型比较永远不成立。
- 修复：按 NCombatBackground.SceneFilePath 精确匹配螃蟹房真实资源路径。路径由 EncounterModel.CreateBackground、BackgroundAssets、SceneHelper 的生产调用链确认。
- 本次生产代码仅修改 CrabRoom.Active 的识别方式；现有朝向、人物锚点、召唤铺屏和暴政放大实现无需再改。召唤铺屏与用户认可的 aca03530 方式等价，仅限定螃蟹房启用。其他房间不会启用适配。
- 39 项生产 CrabRoom 离线替身检查：通用容器正确识别螃蟹；左右换向；缩放只换算一次；普通房间偏移和大小不变、不创建屏幕层；进出房间不会缓存错误状态；屏幕层不重复嵌套。
- Release 零警告零错误。0.111.0 / 0.107.1 各 1203 处引用全部兼容。不启动游戏，不用 Computer Use；视觉结果由用户反馈。
- 资源包保持 SHA256 40E7EF38024C2F572A7202C1D50AAC2BFBA268936328C0426F4B35C5542D4E66，无卡面/卡框替换资源回归。版本仍为 0.14.105。
