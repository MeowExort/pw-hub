-- Example: reload current page
Print('Reloading page...')
Browser_ReloadCb(function(ok)
  Print('Reload requested: ' .. tostring(ok))
end)
