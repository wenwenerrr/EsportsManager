import json, sys
root = sys.argv[1]
LOSS_SCALE = 2.5  # 2026-10-20 user decision: every negative funds effect x2.5, windfalls unchanged
def fx(t, target, a, b=None):
    if t == "funds" and a < 0: a = a * LOSS_SCALE; b = None if b is None else b * LOSS_SCALE
    return {"type": t, "target": target, "min": float(a), "max": float(a if b is None else b)}
def S(t, a, b=None): return fx(t, "subject", a, b)
def C(t, a, b=None): return fx(t, "club", a, b)
def cond(t, v, trait=""): return {"type": t, "value": float(v), "trait": trait}
EV = []; TX = {}
def ev(id, kind, tone, action, subject, weight, title, text, effects=(), conds=(), chance=1.0, options=(), default=0):
    o = []
    for i, (label, efs) in enumerate(options):
        key = "evt.%s.opt%d" % (id, i); TX[key] = label
        o.append({"label_key": key, "effects": list(efs)})
    EV.append({"id": id, "kind": kind, "tone": tone, "action": action, "subject": subject, "weight": float(weight),
               "chance": float(chance), "title_key": "evt.%s.title" % id, "text_key": "evt.%s.text" % id,
               "conditions": list(conds), "effects": list(effects), "options": o, "default_option": default})
    TX["evt.%s.title" % id] = title; TX["evt.%s.text" % id] = text
A = lambda id, tone, act, w, ti, tx, efs: ev(id, "action", tone, act, "team" if act == "sponsor" else "player", w, ti, tx, efs)
# ---- train
A("train_breakthrough", "good", "train", 1, "突然开窍", "{player} 练着练着忽然一拍大腿：“原来是这么回事！”这一练顶两练，天花板好像也高了一截。", [S("train_multiplier", 2), S("potential", 5, 15)])
A("train_hot_hands", "good", "train", 4, "手感火热", "{player} 今天手感好得离谱，练的那一项肉眼可见地进步了。", [S("train_add", 1.5, 2.5)])
A("train_coach_praise", "good", "train", 3, "教练点名表扬", "教练当着全队夸了 {player} 一句，他练得更起劲了。", [S("train_multiplier", 1.3), S("mood", 4, 8)])
A("train_scrim_crushed", "bad", "train", 4, "训练赛被虐", "{player} 在训练赛里被对面按在地上摩擦，复盘时脸都是黑的。", [S("stress", 6, 10), S("mood", -5, -3)])
A("train_sore_wrist", "bad", "train", 3, "手腕酸痛", "{player} 练太久手腕疼，去做了理疗，这次训练只练了一半。", [S("train_multiplier", 0.5), S("stress", 5), C("funds", -6000, -3000)])
A("train_pc_broken", "bad", "train", 3, "电脑冒烟了", "{player} 的电脑在关键时刻冒出一股青烟。换新主机花了一笔钱，训练也被耽误了。", [S("train_multiplier", 0.7), C("funds", -12000, -5000)])
# ---- stream
A("stream_viral", "good", "stream", 3, "直播出圈了", "{player} 直播里一个神操作被剪成短视频疯传，直播间挤满了新观众。", [S("stream_multiplier", 2), S("player_popularity", 3, 6)])
A("stream_big_tipper", "good", "stream", 2, "榜一大哥", "一位神秘观众在 {player} 的直播间连刷礼物，留言只有四个字：“好好打球。”", [C("funds", 3000, 8000)])
A("stream_fan_art", "good", "stream", 3, "粉丝送画", "粉丝给 {player} 画了一张帅气的同人图，他开心了一整晚。", [S("mood", 5, 8), S("player_popularity", 1, 2)])
A("stream_slip", "bad", "stream", 3, "直播口误", "{player} 直播时说错话被截图，评论区吵成一锅粥。", [S("player_popularity", -4, -2), S("stress", 4, 6), C("club_popularity", -1)])
A("stream_trolls", "bad", "stream", 3, "黑粉带节奏", "一群黑粉涌进 {player} 的直播间刷屏嘲讽，他下播后一句话也不想说。", [S("mood", -12, -8)])
A("stream_platform_fine", "bad", "stream", 3, "直播被罚款", "{player} 直播时背景放了没有版权的歌，平台罚款，这场收入也打了折。", [S("stream_multiplier", 0.5), C("funds", -8000, -4000)])
# ---- rest
A("rest_slept_well", "good", "rest", 4, "睡了个好觉", "{player} 一觉睡到自然醒，整个人都轻松了。", [S("stress", -12, -8), S("mood", 3, 5)])
A("rest_family_visit", "good", "rest", 2, "家人来探班", "{player} 的家人带着家乡菜来基地看他，他笑得合不拢嘴。", [S("mood", 6, 10)])
A("rest_secret_ranked", "bad", "rest", 3, "偷偷打排位", "说好休息，{player} 却偷偷打了一整天排位。玩得挺开心，就是没休息好。", [S("rest_multiplier", 0.5), S("mood", 2)])
A("rest_food_poisoning", "bad", "rest", 2, "吃坏肚子", "{player} 休息日吃了路边摊，进了趟医院，医药费俱乐部出。", [S("rest_multiplier", 0.6), S("mood", -4), C("funds", -6000, -3000)])
# ---- business
A("biz_ad_viral", "good", "business", 3, "广告爆了", "{player} 拍的广告意外走红，选手和俱乐部都涨了一波人气。", [S("player_popularity", 3, 5), C("club_popularity", 1, 2)])
A("biz_bonus", "good", "business", 2, "甲方加钱", "甲方对 {player} 的表现非常满意，主动把报酬提高了一半。", [S("business_multiplier", 1.5)])
A("biz_exhausting_shoot", "bad", "business", 4, "拍摄太累", "广告拍了十几条才过，{player} 回到基地直接瘫在沙发上。", [S("stress", 6, 10)])
A("biz_contract_penalty", "bad", "business", 2, "迟到赔违约金", "{player} 拍摄迟到两小时，合同里写着要赔违约金。", [C("funds", -12000, -6000), S("mood", -3)])
# ---- sponsor
A("sponsor_boss_dinner", "good", "sponsor", 4, "老板饭局", "饭桌上聊得很投机，对方老板说：“回去就让人拟合同。”", [S("sponsor_chance_add", 0.15)])
A("sponsor_deposit", "good", "sponsor", 1, "意向金到账", "对方还没决定签不签长约，但先打来了一笔“意向金”。", [C("funds", 5000, 10000)])
A("sponsor_stood_up", "bad", "sponsor", 3, "被放鸽子", "约好的赞助商临时说来不了了，白跑一趟。", [S("void_action", 0)])
A("sponsor_dinner_bill", "bad", "sponsor", 3, "饭局你买单", "谈赞助的饭局，对方点了最贵的龙虾，最后是俱乐部买单。", [C("funds", -8000, -4000), S("sponsor_chance_add", -0.05)])
# ---- state
ST = lambda id, tone, subj, w, ch, ti, tx, conds, efs: ev(id, "state", tone, "", subj, w, ti, tx, efs, conds, ch)
ST("state_breakdown", "bad", "player", 3, 0.6, "情绪崩溃", "压力太大了，{player} 训练时突然摔了键盘，躲进房间不出来。", [cond("stress_above", 80)], [S("mood", -20, -15)])
ST("state_pair_quarrel", "bad", "pair", 3, 0.4, "队友吵架", "{player} 和 {partner} 因为一波团战谁该先上吵了起来，两人现在谁也不理谁。", [cond("pair_bad_compat", -0.3)], [S("familiarity", -12, -8), S("mood", -3)])
ST("state_breakthrough", "good", "starter", 2, 0.25, "开窍", "状态正好的 {player} 好像突然看懂了这个游戏，比以前更有潜力了。", [cond("mood_above", 75), cond("stress_below", 50)], [S("potential", 5, 15), S("mood", 3)])
ST("state_diligent_grind", "good", "player", 1, 0.15, "苦练见效", "勤奋的 {player} 每天最后一个离开训练室，终于练出了新东西。", [cond("trait_has", 0, "diligent"), cond("mood_above", 60)], [S("potential", 5, 10)])
ST("state_slump", "bad", "player", 2, 0.4, "陷入低谷", "{player} 心情差、压力大，怎么打都不对劲，开始怀疑自己。", [cond("stress_above", 70), cond("mood_below", 40)], [S("potential", -8, -3), S("mood", -5)])
ST("state_losing_streak", "bad", "team", 2, 0.5, "连败挨骂", "连输几场，网上骂声一片，队员们都不敢看手机。", [cond("recent_loss_streak", 3)], [fx("mood", "starters", -8, -5), C("club_popularity", -2)])
ST("state_win_hype", "good", "team", 2, 0.4, "周边卖爆了", "战绩亮眼，俱乐部的周边被抢购一空。", [cond("recent_win_rate_above", 65)], [C("club_popularity", 2, 4), C("funds", 5000, 10000)])
ST("state_team_bonding", "good", "team", 2, 0.3, "团建", "大家默契不够，经理组织去吃了顿火锅。花了点钱，但关系近多了。", [cond("chemistry_below", 40)], [fx("familiarity", "starters", 4, 6), C("funds", -8000, -5000)])
ST("state_fan_meetup_loss", "bad", "team", 2, 0.25, "粉丝见面会亏本", "办了场粉丝见面会，来的人比预想少一半，场地费照付。", [cond("club_popularity_at_least", 40)], [C("funds", -12000, -6000), C("club_popularity", 1)])
ST("state_fire_inspection", "bad", "team", 1, 0.3, "消防检查罚款", "消防检查发现基地的灭火器过期了，罚款。", [], [C("funds", -6000, -3000)])
ST("state_medical", "bad", "starter", 2, 0.4, "腱鞘炎", "{player} 的手腕查出了腱鞘炎，治疗费不便宜。", [cond("stress_above", 75)], [C("funds", -10000, -5000), S("stress", 5)])
# ---- choice
CH = lambda id, subj, w, ti, tx, conds, opts, d: ev(id, "choice", "neutral", "", subj, w, ti, tx, (), conds, 1.0, opts, d)
CH("choice_home_leave", "player", 3, "想请假回家", "{player} 说家里有事，想请假回家一趟。", [],
   [("同意（本赛段不能安排他的行动）", [S("on_leave", 0), S("mood", 10, 15), S("stress", -10)]), ("拒绝", [S("mood", -10, -6)])], 1)
CH("choice_extra_stream", "player", 3, "赞助商要求加场直播", "赞助商希望 {player} 临时加一场带货直播。", [],
   [("接受", [C("funds", 6000, 10000), S("stress", 6, 8)]), ("拒绝", [C("club_popularity", -2, -1)])], 1)
CH("choice_equipment_upgrade", "team", 2, "要不要换新设备", "队员们说训练室的设备太旧了，想全换新的。", [],
   [("买！", [C("funds", -20000, -15000), fx("mood", "roster", 4)]), ("再等等", [fx("mood", "roster", -2)])], 1)
CH("choice_variety_show", "player", 2, "综艺节目邀请", "一档综艺节目想请 {player} 去当嘉宾。", [],
   [("去", [S("player_popularity", 4, 6), C("club_popularity", 1, 2), S("stress", 8, 10), C("funds", 3000, 5000)]), ("不去", [])], 1)
CH("choice_teammate_conflict", "pair", 2, "找你评理", "{player} 和 {partner} 闹了矛盾，一起来找你评理。", [cond("pair_bad_compat", -0.3)],
   [("各打五十大板", [S("mood", -3), S("familiarity", 5)]), ("让他们自己解决", [S("familiarity", -6)])], 1)

cfg = {"action_events": {"p_event": 0.25, "max_per_segment": 3},
       "good_chance": {"base": 0.55, "mood_weight": 0.003, "stress_weight": 0.004, "neutral": 50.0, "min": 0.2, "max": 0.85},
       "max_trait_modifier": 0.05,
       "trait_modifiers": [{"trait": t, "value": v} for t, v in [("optimistic", 0.05), ("pessimistic", -0.05), ("lively", 0.03), ("reserved", -0.02),
           ("diligent", 0.03), ("lazy", -0.03), ("resolute", 0.02), ("fragile", -0.03), ("clever", 0.02), ("hesitant", -0.02)]],
       "state_events": {"max_per_segment": 1}, "state_attempt_chance": 0.5, "choice_events": {"p_choice": 0.3, "max_per_segment": 1}, "events": EV}
open(root + "/Assets/Data/event_config.json", "w", encoding="utf-8").write(json.dumps(cfg, ensure_ascii=False, indent=2) + "\n")
open(root + "/Assets/Data/event_text_zh.json", "w", encoding="utf-8").write(json.dumps({"texts": [{"key": k, "text": v} for k, v in TX.items()]}, ensure_ascii=False, indent=2) + "\n")
print(len(EV), "events", len(TX), "texts")
