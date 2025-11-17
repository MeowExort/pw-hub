// promo_ping.js — модуль Promo.Ping (одноразовый пинг при инжекции)
(function(){
  try{
    function doPing(){
      try{
        if (window.chrome && window.chrome.webview && window.chrome.webview.postMessage) {
          window.chrome.webview.postMessage(JSON.stringify({ type: 'promo_ping2', ts: String(Date.now()) }));
        }
      }catch(_){ }
    }

    if (!window.Promo || !Promo.register){
      // legacy: выполнить сразу
      doPing();
      return;
    }

    Promo.register('Ping', { run: doPing });
  }catch(_){ }
})();