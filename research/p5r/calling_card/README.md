# P5R 原版预告信贴图

优先使用道具模型贴图做旋转卡背。四种道具贴图的上半部是标志面，下半部是平铺的剪报正文。鸭志田卓、奥村邦和、新岛冴本次只在原版画廊找到，两面已排成带透视的展示图。

| 导出文件 | 尺寸 | 来源与内容 |
| --- | --- | --- |
| IT0164_001__it0164_001.png | 256 × 256 | BASE.CPK / MODEL/ITEM/IT0164_001.GMD / it0164_001.dds，金城润矢，标志面及正文 |
| IT0165_001__it0165_t01.png | 256 × 256 | BASE.CPK / MODEL/ITEM/IT0165_001.GMD / it0165_t01.DDS，佐仓双叶，标志面及正文 |
| IT0286_000__it0286_t01.png | 256 × 256 | BASE.CPK / MODEL/ITEM/IT0286_000.GMD / it0286_t01.dds，斑目流斋，标志面及正文 |
| IT0286_001__it0286_t01.png | 256 × 256 | BASE.CPK / MODEL/ITEM/IT0286_001.GMD / it0286_t01.dds，同上 |
| IT0286_002__it0286_t01.png | 256 × 256 | BASE.CPK / MODEL/ITEM/IT0286_002.GMD / it0286_t01.dds，同上 |
| IT0554_000__it0286_t01.png | 256 × 256 | BASE.CPK / MODEL/ITEM/IT0554_000.GMD / it0286_t01.dds，同上 |
| IT0815_000__it0815_t01.png | 256 × 256 | BASE.CPK / MODEL/ITEM/IT0815_000.GMD / it0815_t01.dds，丸喜拓人，标志面及正文 |
| IT0173_002__it0173_t01.png | 512 × 512 | BASE.CPK / MODEL/ITEM/IT0173_002.GMD / it0173_t01.DDS，印有「予告状」的红色卡片及火漆信封，无拼贴正文 |
| gallery__IMG097.png | 4096 × 4096 | BASE.CPK / MYPALACE/GALLERY/IMG/IMG097.DDS，鸭志田卓，两面展示 |
| gallery__IMG098.png | 4096 × 4096 | BASE.CPK / MYPALACE/GALLERY/IMG/IMG098.DDS，斑目流斋，两面展示 |
| gallery__IMG099.png | 4096 × 4096 | BASE.CPK / MYPALACE/GALLERY/IMG/IMG099.DDS，金城润矢，两面展示 |
| gallery__IMG100.png | 4096 × 4096 | BASE.CPK / MYPALACE/GALLERY/IMG/IMG100.DDS，佐仓双叶，两面展示 |
| gallery__IMG101.png | 4096 × 4096 | BASE.CPK / MYPALACE/GALLERY/IMG/IMG101.DDS，奥村邦和，两面展示 |
| gallery__IMG102.png | 4096 × 4096 | BASE.CPK / MYPALACE/GALLERY/IMG/IMG102.DDS，新岛冴，两面展示 |
| gallery__IMG126.png | 4096 × 4096 | BASE.CPK / MYPALACE/GALLERY/IMG/IMG126.DDS，丸喜拓人，两面展示 |

斑目的四个导出 PNG 的 SHA-256 相同，按来源分别保留。found 下的 PNG 均从原 DDS 解码为 RGBA，保留尺寸、布局与透明度，未量化、裁切或修正透视。

从 BASE.CPK、SC.CPK 共提取 1,208 个道具 GMD、200 个画廊缩略图与 7 个对应大图。使用本机 P5RAssetTool 和 GFDLibrary 读取资源，再用本机 Pillow 导出 2,945 张嵌入贴图。先匹配文件名 card、yokoku、yokokujo、kokuchi、letter、tegami、kaitou，再按红黑白配色和已确认的标志面筛查道具贴图，并查看 200 张画廊缩略图。前两项已找到正文，按任务单跳过场景 BIN。

本机原始与转换文件约 1.52 GB。仓库新增约 65.8 MB，含贴图索引、30 张道具联系表、1 张画廊联系表及 15 张原尺寸 PNG。最大单文件约 3.19 MB，最大道具联系表约 2.08 MB。

## 提交状态

基于 ab788364。初次暂存因稀疏检出范围受阻，按任务单暂停。收到确认后，仅暂存本任务目录，素材提交为 39ef1956。
