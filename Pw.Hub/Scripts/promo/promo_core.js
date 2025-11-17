(function(){
  try{
    if (window.Promo && window.Promo.__v) return;

    var modules = [];
    var readyQueue = [];
    var isReady = document.readyState === 'complete' || document.readyState === 'interactive';

    // Небольшой диагностический логгер JS → .NET (WebView2)
    function promoLog(eventName, payload){
      try{
        var msg = { type: 'promo_log', event: 'core_' + String(eventName||''), data: payload || null, ts: Date.now() };
        if (window.chrome && window.chrome.webview && window.chrome.webview.postMessage){
          window.chrome.webview.postMessage(JSON.stringify(msg));
        }
      }catch(_){ }
    }

    function flushReady(){
      if (!isReady) return;
      try{
        var q = readyQueue.slice();
        readyQueue.length = 0;
        try{ promoLog('ready_flush', { count: q.length }); }catch(_){ }
        q.forEach(function(cb){ try{ cb(); }catch(_){ } });
      }catch(_){ }
    }

    try{
      document.addEventListener('DOMContentLoaded', function(){
        isReady = true;
        try{ promoLog('dom_ready', null); }catch(_){ }
        flushReady();
      }, { once: true });
    }catch(_){ }

    window.Promo = {
      __v: 1,
      Modules: modules,
      register: function(name, runner){
        try{
          var modName = name || ('mod_'+modules.length);
          // Normalize runner: accept function or object with .run()
          var runFn = null;
          try{
            if (typeof runner === 'function') {
              runFn = runner;
            } else if (runner && typeof runner.run === 'function') {
              // bind to preserve potential this, but call without args
              runFn = function(){ try{ return runner.run(); }catch(__){} };
            }
          }catch(__){}
          try{ promoLog('register', { name: modName, kind: (typeof runner), hasRun: !!runFn }); }catch(_){ }
          modules.push({ name: modName, run: runFn });
        }catch(_){ }
      },
      autoRun: function(){
        try{
          try{ promoLog('autorun_start', { count: modules.length }); }catch(_){ }
          modules.forEach(function(m){
            try{
              if (m && typeof m.run === 'function'){
                try{ promoLog('run_before', { name: m.name }); }catch(_){ }
                try{
                  m.run();
                  try{ promoLog('run_after', { name: m.name }); }catch(_){ }
                }catch(ex){
                  try{ promoLog('run_err', { name: m.name, message: (ex && ex.message) ? ex.message : String(ex) }); }catch(_){ }
                }
              } else {
                try{ promoLog('run_skip', { name: m && m.name ? m.name : '(unknown)', reason: 'no function' }); }catch(_){ }
              }
            }catch(_){ }
          });
          try{ promoLog('autorun_done', { count: modules.length }); }catch(_){ }
        }catch(_){ }
      },
      ready: function(cb){
        if (!cb) return;
        if (isReady) {
          try{ promoLog('ready_immediate', null); }catch(_){ }
          try{ cb(); }catch(_){ }
        }
        else {
          try{ promoLog('ready_queue_push', null); }catch(_){ }
          readyQueue.push(cb);
        }
      }
    };

    try{ promoLog('loaded', { isReady: isReady }); }catch(_){ }
  }catch(_){ }
})();
