// promo_init.js — базовая инициализация промо-страницы
(function(){
  try{
    var qs = (location.search||'').toLowerCase();
    var isActivate = qs.indexOf('do=activate') !== -1;
    if (!isActivate){
      // Снять стандартные клики с чекбоксов (jQuery/vanilla)
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

    // Разделитель-контейнер после .promo_container_content_body
    try {
      if (!document.getElementById('promo_separator')){
        var element = document.createElement('div');
        element.id = 'promo_separator';
        var host = document.querySelector('.promo_container_content_body');
        if (host && host.parentElement) host.parentElement.insertBefore(element, host.nextSibling);
      }
    } catch(_){}
  }catch(e){}
})();
