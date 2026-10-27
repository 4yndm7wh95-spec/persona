from pathlib import Path
# Reuse the previous packing/validation workflow without changing the previous review files.
s=Path(__file__).with_name('summon_right15_finish.py').read_text(encoding='utf-8')
s=s.replace('right15','right30').replace('336.856095851','351.856095851').replace('336.86','351.86')
s=s.replace('从321.856095851°转至351.856095851°（+15°）','从336.856095851°转至351.856095851°（本次+15°，累计+30°）')
s=s.replace('用户反馈“后面两帧往人物右边转一些”','用户反馈“再多转15度”')
exec(compile(s,__file__,'exec'))
