comp-kitchen-spike-begin-hook-self = Вы начинаете насаживать себя на { $hook }!
comp-kitchen-spike-begin-hook-self-other = { CAPITALIZE($victim) } начинает насаживать { REFLEXIVE($victim) } себя на { $hook }!
comp-kitchen-spike-begin-hook-other-self = Вы начинаете насаживать себя { CAPITALIZE($victim) } на { $hook }!
comp-kitchen-spike-begin-hook-other = { CAPITALIZE($user) } начинает насаживать { CAPITALIZE($victim) } на { $hook }!
comp-kitchen-spike-hook-self = Вы бросаетесь на { $hook }!
comp-kitchen-spike-hook-self-other = { CAPITALIZE($victim) } бросается на { $hook }!
comp-kitchen-spike-hook-other-self = Вы повесили { CAPITALIZE($victim) } на { $hook }!
comp-kitchen-spike-hook-other =
    { CAPITALIZE($user) } { GENDER($user) ->
        [male] повесил
        [female] повесила
        [epicene] повесили
       *[neuter] повесило
    } { CAPITALIZE($victim) } на { $hook }!
comp-kitchen-spike-begin-unhook-self = Вы начинаете слезать с { $hook }!
comp-kitchen-spike-begin-unhook-self-other = { CAPITALIZE($victim) } начинает слезать с { $hook }!
comp-kitchen-spike-begin-unhook-other-self = Вы начинаете снимать { CAPITALIZE($victim) } с { $hook }!
comp-kitchen-spike-begin-unhook-other = { CAPITALIZE($user) } начинает снимать { CAPITALIZE($victim) } с { $hook }!
comp-kitchen-spike-unhook-self = Вы слезли с { $hook }!
comp-kitchen-spike-unhook-self-other = { CAPITALIZE($victim) } слез с { $hook }!
comp-kitchen-spike-unhook-other-self = Вы сняли { CAPITALIZE($victim) } с { $hook }!
comp-kitchen-spike-unhook-other =
    { CAPITALIZE($user) } { GENDER($user) ->
        [male] снял
        [female] сняла
        [epicene] сняли
       *[neuter] сняло
    } { CAPITALIZE($victim) } с { $hook }!
comp-kitchen-spike-begin-butcher-self = Вы начинаете разделывать { $victim }!
comp-kitchen-spike-begin-butcher = { CAPITALIZE($user) } начинает разделывать { $victim }!
comp-kitchen-spike-butcher-self = Вы разделали { $victim }!
comp-kitchen-spike-butcher =
    { CAPITALIZE($user) } { GENDER($user) ->
        [male] разделал
        [female] разделала
        [epicene] разделали
       *[neuter] разделало
    } { $victim }!
comp-kitchen-spike-unhook-verb = Снять с крюка
comp-kitchen-spike-hooked = [color=red]На крюке { CAPITALIZE($victim) }![/color]
comp-kitchen-spike-meat-name = { $name } ({ $victim })
comp-kitchen-spike-victim-examine = [color=orange]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-BASIC($target, "выглядят", "выглядит") } довольно { GENDER($target) ->
        [male] худым
        [female] худой
        [epicene] худыми
       *[neuter] худым
    }.[/color]
comp-kitchen-spike-deny-collect = { CAPITALIZE(THE($this)) } already has something on it, finish collecting its meat first!
comp-kitchen-spike-deny-butcher-knife = { CAPITALIZE(THE($victim)) } can't be butchered on { THE($this) }, you need to butcher it using a knife.
comp-kitchen-spike-kill = { CAPITALIZE(THE($user)) } has forced { THE($victim) } onto the spike, killing them instantly!
comp-kitchen-spike-suicide-other = { CAPITALIZE(THE($victim)) } has thrown themselves on a meat spike!
comp-kitchen-spike-deny-not-dead = { CAPITALIZE(THE($victim)) } can't be butchered. { CAPITALIZE(SUBJECT($victim)) } { CONJUGATE-BE($victim) } is not dead!
comp-kitchen-spike-knife-needed = You need a knife to do this.
comp-kitchen-spike-begin-hook-victim = { CAPITALIZE(THE($user)) } begins dragging you onto { THE($this) }!
comp-kitchen-spike-remove-meat = You remove some meat from { THE($victim) }.
comp-kitchen-spike-suicide-self = You throw yourself on a meat spike!
comp-kitchen-spike-remove-meat-last = You remove the last piece of meat from { THE($victim) }!
comp-kitchen-spike-deny-butcher = { CAPITALIZE(THE($victim)) } can't be butchered on { THE($this) }.
