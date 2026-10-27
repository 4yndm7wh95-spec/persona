# 倒下及侧躺关键帧 die_fall_a / die_down_a（2026-10-27）

开始前fetch + merge成功，远端与本地2502bd4f一致。两张均以已认可die_kneel_a2为唯一底图编辑，没有将新生成的fall作为down底图。未rebase/reset/强推。

- die_fall_a：提示词要求撑地右臂一软、向前往画面右方侧倒半程，右肩右臂接近地面，上身斜向右下、头在右且低于胯，刘海遮眼，左手从膝滑落，两腿保持弯曲、左膝下倒，衣摆散开。原图提交4b0d89de，清理主体外alpha1～2残点12像素。
- die_down_a：提示词要求右侧身体贴地，头右脚左、身体横于画布下部，脸朝镜头眼闭，腿微弯叠放，右臂身下或前伸地面、左手身前落地，衣摆铺盖，横向完整放入1024宽且留边。原图提交7d912b3c，清理残点210像素。

两张均强调头、手、身体比例、服装和地面高度照die_kneel_a2，头手不放大缩小，不画特效、伤口和血。以上为提示词约束，实际姿势交给用户判断。完整提示词、输入、原始输出路径见各自_generation.json。内置image_gen各生成一次，未审图重画，保存1024×1536 RGBA。每张保存即提交推送，并用ls-remote确认origin包含提交。

新图均登记1.0/front，现有归一化函数仅对本批两张运行一次并合并normalize.json，保留其他final及既有参数，不加额外缩放位移。

## 一次比例检查（输出原样记录）

`python tools/p5/pose_consistency.py idle_a3 die_fall_a die_down_a`

```text
Traceback (most recent call last):
  File "C:\Users\kkk\Documents\Codex\2026-10-24\docs-codex-codex-p5r-rip-md\work\check_die_fall_down.py", line 9, in <module>
    with contextlib.redirect_stdout(s):runpy.run_path(str(r/'tools/p5/pose_consistency.py'),run_name='__main__')
                                       ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
  File "<frozen runpy>", line 287, in run_path
  File "<frozen runpy>", line 98, in _run_module_code
  File "<frozen runpy>", line 88, in _run_code
  File "C:\Users\kkk\Documents\Codex\2026-10-12\claude-git-c-users-kkk-videos\work\persona-ref-upload\tools\p5\pose_consistency.py", line 127, in <module>
    main()
  File "C:\Users\kkk\Documents\Codex\2026-10-12\claude-git-c-users-kkk-videos\work\persona-ref-upload\tools\p5\pose_consistency.py", line 104, in main
    hh = head_height(a) / base_h
         ^^^^^^^^^^^^^^
  File "C:\Users\kkk\Documents\Codex\2026-10-12\claude-git-c-users-kkk-videos\work\persona-ref-upload\tools\p5\pose_consistency.py", line 54, in head_height
    i = int(np.argmax(ndimage.sum(skin, lab, range(1, k + 1)))) + 1
            ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
  File "C:\Users\kkk\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\Lib\site-packages\numpy\_core\fromnumeric.py", line 1341, in argmax
    return _wrapfunc(a, 'argmax', axis=axis, out=out, **kwds)
           ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
  File "C:\Users\kkk\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\Lib\site-packages\numpy\_core\fromnumeric.py", line 57, in _wrapfunc
    return bound(*args, **kwds)
           ^^^^^^^^^^^^^^^^^^^^
ValueError: attempt to get argmax of an empty sequence
```

本次检查在首张的head_height()识别皮肤连通域时遇到空序列报错，未产生两张的比例数值或通过结论，也未生成检查拼图。报错原样记录在本机outputs/p5_die_fall_down及本报告，不根据结果重画或重跑。本轮未运行sequence_check。先只做这两张，等用户看过再做过渡帧。

art/cards另一会话11个已修改文件原样保留，未提交、未stash、未还原；guard_a及其他无关资源保留。未打开结城理mod图片，未读取obs.json或密码。最终登记和报告推送后核对origin。
