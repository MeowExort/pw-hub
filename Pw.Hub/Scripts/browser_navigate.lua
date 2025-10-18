-- Example: navigate to PW promo page
Print('Navigating to promo items page...')
Browser_NavigateCb('https://pwonline.ru/promo_items.php', function(ok)
  Print('Navigate started: ' .. tostring(ok))
end)
