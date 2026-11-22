import collections
import json
import pathlib
import sys

source, target = map(pathlib.Path, sys.argv[1:3])
rows = [json.loads(line) for line in source.read_text(encoding='utf-8-sig').splitlines() if line.strip()]
config = json.loads(pathlib.Path(str(source) + '.config.json').read_text(encoding='utf-8-sig'))
assert len(rows) == config['builds'] * config['repeats'] * 2, 'Incomplete run'
assert len({(r['build'], r['repeat'], r['apLeft']) for r in rows}) == len(rows), 'Duplicate trial'
assert all(abs(r['deltaTime'] - config['step']) < 1e-5 for r in rows), 'Variable timestep'
assert all(r['winner'] in ('AP', 'HE', 'draw', 'timeout') for r in rows), 'Invalid outcome'
counts = collections.Counter(r['winner'] for r in rows)
lines = ['# AP・HEランダム建造比較', '', f"設定: `{json.dumps(config)}`", '',
         f"{len(rows)}戦: AP {counts['AP']}勝 / HE {counts['HE']}勝 / 同時撃沈 {counts['draw']} / 時間切れ {counts['timeout']}", '',
         '|建造ID|砲種×指定数|指定装甲数 / HP ID / 厚ID|実生成区画数|コアHP|AP勝|HE勝|未決着|', '|---|---|---|---|---|---|---|---|']
stable = collections.defaultdict(list)
for b in range(config['builds']):
    group = [r for r in rows if r['build'] == b]
    d = group[0]['design']; a = d['armor'][0]; c = collections.Counter(r['winner'] for r in group)
    assert len(group) == config['repeats'] * 2 and all(r['design'] == d for r in group), 'Unmatched builds'
    gun = next(i for i, n in enumerate(d['weaponCounts']) if n)
    lines.append(f"|{b}|{['小型', '大型'][gun]}×{d['weaponCounts'][gun]}|{a['count']} / {a['hpSettingID']} / {a['defenseThicknessSettingID']}|{group[0]['initialSections']}|{group[0]['initialHP']}|{c['AP']}|{c['HE']}|{c['draw']+c['timeout']}|")
    for ammo in ('AP', 'HE'):
        if c[ammo] == len(group): stable[ammo].append(b)
lines += ['', f"全反復・左右でAP勝利: {stable['AP']}", f"全反復・左右でHE勝利: {stable['HE']}",
          '条件間の逆転を確認。' if stable['AP'] and stable['HE'] else '両弾種がそれぞれ全反復・左右で勝つ条件間の逆転は今回未確認。', '']
for side in (True, False):
    lines.append(f"APが{'左' if side else '右'}: {dict(collections.Counter(r['winner'] for r in rows if r['apLeft'] == side))}")
target.write_text('\n'.join(lines) + '\n', encoding='utf-8')
print('\n'.join(lines))
