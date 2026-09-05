var h = React.createElement;

var styles = "\
.screen { width: 100%; height: 100%; background-color: rgba(7, 2, 18, 0.96); align-items: center; justify-content: flex-start; padding: 72px 64px 52px; color: white; overflow: hidden; }\
.glow { position: absolute; border-radius: 999px; opacity: 0.55; }\
.glow-a { width: 620px; height: 620px; left: -210px; top: 120px; background-color: rgba(148, 30, 255, 0.28); box-shadow: 0 0 90px rgba(170, 40, 255, 0.7); }\
.glow-b { width: 560px; height: 560px; right: -190px; bottom: 170px; background-color: rgba(255, 168, 28, 0.18); box-shadow: 0 0 80px rgba(255, 188, 35, 0.55); }\
.header { width: 100%; height: 152px; flex-direction: row; align-items: center; justify-content: space-between; }\
.titleBlock { align-items: flex-start; justify-content: center; }\
.eyebrow { color: #f7c35d; font-size: 28px; font-weight: 700; letter-spacing: 2px; }\
.title { color: white; font-size: 60px; font-weight: 900; text-stroke: 0.55px #4d1b80; }\
.closeButton { width: 76px; height: 76px; border-radius: 38px; background-color: rgba(255, 255, 255, 0.09); border: 2px solid rgba(255, 214, 92, 0.85); align-items: center; justify-content: center; }\
.closeText { color: #ffd760; font-size: 38px; font-weight: 900; }\
.balanceCard { width: 100%; height: 116px; border-radius: 24px; background-color: rgba(18, 9, 36, 0.88); border: 2px solid rgba(255, 214, 92, 0.56); flex-direction: row; align-items: center; justify-content: space-between; padding: 0 36px; box-shadow: 0 12px 32px rgba(0, 0, 0, 0.45); }\
.balanceLabel { color: #b790ff; font-size: 30px; font-weight: 800; }\
.balanceValue { color: #ffd760; font-size: 38px; font-weight: 900; }\
.stage { width: 704px; height: 760px; margin-top: 58px; align-items: center; justify-content: center; }\
.pointer { position: absolute; top: 26px; width: 94px; height: 94px; border-radius: 47px; background-color: rgba(10, 5, 22, 0.92); border: 3px solid #ffd75a; align-items: center; justify-content: center; z-index: 8; box-shadow: 0 0 30px rgba(255, 205, 74, 0.85); }\
.pointerTip { width: 42px; height: 42px; rotate: 45deg; background-color: #ffd75a; border-radius: 8px; }\
.wheel { width: 588px; height: 588px; border-radius: 294px; background-color: rgba(25, 8, 45, 0.98); border: 8px solid #ffc83d; position: relative; align-items: center; justify-content: center; box-shadow: 0 0 46px rgba(186, 54, 255, 0.9); }\
.wheelActive { transition: rotate 2.8s cubic-bezier(0.12, 0.82, 0.16, 1); }\
.wheelInner { position: absolute; width: 396px; height: 396px; border-radius: 198px; background-color: rgba(255, 255, 255, 0.04); border: 3px solid rgba(255, 255, 255, 0.12); }\
.reward { position: absolute; width: 156px; height: 96px; border-radius: 22px; border: 3px solid rgba(255, 255, 255, 0.82); align-items: center; justify-content: center; box-shadow: 0 9px 18px rgba(0, 0, 0, 0.42); }\
.reward-0 { background-color: #4f2cff; }\
.reward-1 { background-color: #b31fff; }\
.reward-2 { background-color: #1557ff; }\
.reward-3 { background-color: #ff7b1c; }\
.reward-4 { background-color: #e02175; }\
.reward-5 { background-color: #2028a8; }\
.rewardTop { color: rgba(255, 255, 255, 0.8); font-size: 18px; font-weight: 700; }\
.rewardValue { color: white; font-size: 34px; font-weight: 900; text-stroke: 0.35px rgba(0, 0, 0, 0.75); }\
.centerBadge { width: 202px; height: 202px; border-radius: 101px; background-color: #140821; border: 5px solid #ffd75a; align-items: center; justify-content: center; box-shadow: 0 0 28px rgba(255, 216, 92, 0.8); }\
.centerIcon { color: #ffd75a; font-size: 74px; font-weight: 900; }\
.centerText { color: white; font-size: 26px; font-weight: 900; }\
.resultCard { width: 100%; min-height: 122px; border-radius: 26px; background-color: rgba(255, 255, 255, 0.08); border: 2px solid rgba(183, 144, 255, 0.45); align-items: center; justify-content: center; padding: 18px 26px; }\
.resultBig { color: #ffd760; font-size: 62px; font-weight: 900; text-stroke: 0.45px #623200; }\
.resultSmall { color: rgba(255, 255, 255, 0.86); font-size: 30px; font-weight: 700; text-align: center; }\
.spinButton { width: 100%; height: 126px; margin-top: 36px; border-radius: 34px; background-color: #ffb21b; border: 4px solid #ffe58c; align-items: center; justify-content: center; box-shadow: 0 13px 0 #8e3b00; }\
.spinButtonDisabled { opacity: 0.58; background-color: #6d5d7a; border-color: rgba(255, 255, 255, 0.42); box-shadow: 0 9px 0 rgba(0, 0, 0, 0.32); }\
.spinText { color: #351300; font-size: 48px; font-weight: 900; text-stroke: 0.55px rgba(255, 255, 255, 0.45); }\
.footerText { margin-top: 30px; color: rgba(255, 255, 255, 0.72); font-size: 28px; font-weight: 700; text-align: center; }\
";

function cx() {
  var result = [];
  for (var i = 0; i < arguments.length; i++) {
    if (arguments[i]) result.push(arguments[i]);
  }
  return result.join(' ');
}

function asRewards(api) {
  try {
    var csv = api.GetRewardsCsv();
    return String(csv).split(',').map(function (x) { return parseInt(x, 10); });
  } catch (err) {
    return [50, 100, 200, 500, 1000, 2000];
  }
}

function callGlobal(name) {
  try {
    if (Globals && typeof Globals[name] === 'function') Globals[name]();
  } catch (err) {}
}

function formatNumber(value) {
  value = Number(value || 0);
  return value.toLocaleString ? value.toLocaleString('pt-BR') : String(value);
}

function rewardPosition(index) {
  var angle = -90 + index * 60;
  var rad = angle * Math.PI / 180;
  var radius = 202;
  var center = 294;
  return {
    left: center + Math.cos(rad) * radius - 78,
    top: center + Math.sin(rad) * radius - 48
  };
}

function App() {
  var api = Globals.RouletteAPI;
  var rewards = React.useMemo(function () { return asRewards(api); }, []);
  var initialCanSpin = true;
  var initialBalance = 0;

  try {
    initialCanSpin = api.CanSpin();
    initialBalance = api.GetBalance();
  } catch (err) {}

  var stateCanSpin = React.useState(initialCanSpin);
  var canSpin = stateCanSpin[0];
  var setCanSpin = stateCanSpin[1];

  var stateBalance = React.useState(initialBalance);
  var balance = stateBalance[0];
  var setBalance = stateBalance[1];

  var stateAngle = React.useState(0);
  var angle = stateAngle[0];
  var setAngle = stateAngle[1];

  var stateSpinning = React.useState(false);
  var spinning = stateSpinning[0];
  var setSpinning = stateSpinning[1];

  var stateReadyToSpin = React.useState(false);
  var readyToSpin = stateReadyToSpin[0];
  var setReadyToSpin = stateReadyToSpin[1];

  var stateResult = React.useState(null);
  var result = stateResult[0];
  var setResult = stateResult[1];

  var stateMessage = React.useState('');
  var message = stateMessage[0];
  var setMessage = stateMessage[1];

  React.useEffect(function () {
    var armTimer = setTimeout(function () { setReadyToSpin(true); }, 550);
    var timer = setInterval(function () {
      try {
        var allowed = api.CanSpin();
        setCanSpin(allowed);
        if (!allowed && !spinning) setMessage('Disponivel em ' + api.GetCooldownText());
        if (allowed && !spinning && !result) setMessage('Giro diario disponivel');
      } catch (err) {}
    }, 1000);
    return function () {
      clearTimeout(armTimer);
      clearInterval(timer);
    };
  }, [spinning, result]);

  function close() {
    callGlobal('PlayClickSound');
    callGlobal('CloseRoulette');
  }

  function spin() {
    if (!readyToSpin) return;
    if (spinning) return;
    callGlobal('PlayClickSound');

    try {
      if (!api.CanSpin()) {
        setCanSpin(false);
        setMessage('Disponivel em ' + api.GetCooldownText());
        return;
      }

      var winner = api.Spin();
      if (winner < 0) {
        setCanSpin(false);
        setMessage('Disponivel em ' + api.GetCooldownText());
        return;
      }

      var finalAngle = angle + 2160 + (360 - winner * 60);
      setResult(null);
      setMessage('Girando...');
      setSpinning(true);
      setAngle(finalAngle);

      setTimeout(function () {
        var amount = rewards[winner] || 0;
        try {
          api.ClaimReward(amount);
          setBalance(api.GetBalance());
          setCanSpin(api.CanSpin());
        } catch (err) {}
        setResult(amount);
        setMessage('Voce ganhou ' + formatNumber(amount) + ' moedas');
        setSpinning(false);
      }, 2850);
    } catch (err) {
      setMessage('Roleta indisponivel agora');
      setSpinning(false);
    }
  }

  return h('view', { className: 'screen' },
    h('style', { scope: ':root' }, styles),
    h('view', { className: 'glow glow-a' }),
    h('view', { className: 'glow glow-b' }),
    h('view', { className: 'header' },
      h('view', { className: 'titleBlock' },
        h('text', { className: 'eyebrow' }, 'PREMIO DIARIO'),
        h('text', { className: 'title' }, 'ROLETA DA SORTE')
      ),
      h('button', { className: 'closeButton', onClick: close }, h('text', { className: 'closeText' }, 'X'))
    ),
    h('view', { className: 'balanceCard' },
      h('text', { className: 'balanceLabel' }, 'SALDO'),
      h('text', { className: 'balanceValue' }, formatNumber(balance) + ' MOEDAS')
    ),
    h('view', { className: 'stage' },
      h('view', { className: 'pointer' },
        h('view', { className: 'pointerTip' })
      ),
      h('view', {
        className: cx('wheel', spinning && 'wheelActive'),
        style: { rotate: angle + 'deg' }
      },
        h('view', { className: 'wheelInner' }),
        rewards.map(function (reward, index) {
          var pos = rewardPosition(index);
          return h('view', {
            key: index,
            className: 'reward reward-' + index,
            style: { left: pos.left, top: pos.top }
          },
            h('text', { className: 'rewardTop' }, 'MOEDAS'),
            h('text', { className: 'rewardValue' }, formatNumber(reward))
          );
        }),
        h('view', { className: 'centerBadge' },
          h('text', { className: 'centerIcon' }, '$'),
          h('text', { className: 'centerText' }, 'GIRE')
        )
      )
    ),
    h('view', { className: 'resultCard' },
      result ? h('text', { className: 'resultBig' }, '+' + formatNumber(result)) : h('text', { className: 'resultSmall' }, message || (canSpin ? 'Toque para girar' : 'Aguarde o proximo giro')),
      result ? h('text', { className: 'resultSmall' }, 'creditado na sua conta') : null
    ),
    h('button', {
      className: cx('spinButton', (!readyToSpin || !canSpin || spinning) && 'spinButtonDisabled'),
      onClick: spin,
      disabled: spinning
    },
      h('text', { className: 'spinText' }, !readyToSpin ? 'ABRINDO...' : (spinning ? 'GIRANDO...' : (canSpin ? 'GIRAR AGORA' : 'AGUARDAR')))
    ),
    h('text', { className: 'footerText' }, canSpin ? 'Um giro gratis por dia' : message)
  );
}
