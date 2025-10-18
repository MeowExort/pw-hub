-- Example: first get current account via Account_GetAccountCb,
-- then inside that callback fetch the accounts array and change to the first one.

Account_GetAccountCb(function(currentName)
  if currentName == nil or currentName == '' then
    Print('Current account is empty (maybe not authorized?)')
  else
    Print('Current account before change: ' .. tostring(currentName))
  end

  -- Now get all accounts and pick the first one
  Account_GetAccountsCb(function(accounts)
    if accounts == nil then
      Print('No accounts returned')
      return
    end

    local first = accounts[1]
    if first == nil then
      Print('Accounts list is empty')
      return
    end

    local targetId = tostring(first.Id)
    local targetName = tostring(first.Name)
    Print('Changing account to first in list: ' .. targetName .. ' (' .. targetId .. ')')

    Account_ChangeAccountCb(targetId, function(ok)
      if not ok then
        Print('Failed to change account to ' .. targetName)
        return
      end
      -- Verify by asking current account again
      Account_GetAccountCb(function(newName)
        Print('Current account after change: ' .. tostring(newName))
      end)
    end)
  end)
end)
