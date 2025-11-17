// promo_init.js — базовая инициализация промо-страницы (модуль Promo.Init)
(function(){
  try{
    if (!window.Promo || !Promo.register) {
      // fallback: старое поведение, если promo_core.js не загружен
      (function(){
        try{
          var qs = (location.search||'').toLowerCase();
          var isActivate = qs.indexOf('do=activate') !== -1;
          if (!isActivate){
            try {
              if (window.jQuery && window.jQuery.fn && window.jQuery.fn.unbind) {
                window.jQuery('.items_container input[type=checkbox]').unbind('click');
              } else if (window.jQuery) {
                window.jQuery('.items_container input[type=checkbox]').off('click');
              } else {
                var inp = document.querySelectorAll('.items_container input[type=checkbox]');
                if (inp && inp.forEach) inp.forEach(function(el){ try{ if (el && el.onclick) el.onclick = null; }catch(_){} });
              }
            } catch(_) {}
          }
        }catch(_){ }
      })();
      return;
    }

    Promo.register('Init', {
      run: function(){
        Promo.ready(function(){
          try{
            var qs = (location.search||'').toLowerCase();
            var isActivate = qs.indexOf('do=activate') !== -1;
            if (!isActivate){
              try {
                if (window.jQuery && window.jQuery.fn && window.jQuery.fn.unbind) {
                  window.jQuery('.items_container input[type=checkbox]').unbind('click');
                } else if (window.jQuery) {
                  window.jQuery('.items_container input[type=checkbox]').off('click');
                } else {
                  var inp = document.querySelectorAll('.items_container input[type=checkbox]');
                  if (inp && inp.forEach) inp.forEach(function(el){ try{ if (el && el.onclick) el.onclick = null; }catch(_){} });
                }
              } catch(_) {}
            }
          }catch(_){ }
        });
      }
    });
  }catch(_){ }
})();
