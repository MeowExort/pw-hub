-- Example: get all accounts via Account_GetAccountsCb (Lua table)
-- Each element is a C# Account object with properties like Id, Name, Email
Account_GetAccountsCb(function(accounts)
  if accounts == nil then
    Print('No accounts returned')
    return
  end

  local count = 0
  for i, acc in ipairs(accounts) do
    count = count + 1
    local id = tostring(acc.Id)
    local name = tostring(acc.Name)
    local email = tostring(acc.Email)
    Print(string.format('#%d: %s (%s) id=%s', i, name, email, id))
  end
  Print('Total accounts: ' .. tostring(count))
end)
