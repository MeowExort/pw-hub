-- Example: execute simple JS and print result
Browser_ExecuteScriptCb('document.title', function(title)
  Print('document.title = ' .. tostring(title))
end)
