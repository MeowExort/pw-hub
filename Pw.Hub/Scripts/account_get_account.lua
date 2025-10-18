Account_GetAccountCb(function(name)
  if name == nil or name == '' then
    Print('Account name is empty (maybe not authorized?)')
  else
    Print('Current account: ' .. tostring(name))
  end
end)
