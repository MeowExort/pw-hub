-- Example: change current account. Uses 'selectedAccountId' provided by C# when available.
if selectedAccountId == nil or selectedAccountId == '' then
  Print('selectedAccountId is not provided from UI. Select an account in the left tree and try again.')
else
  Print('Changing account to ' .. tostring(selectedAccountId))
  Account_ChangeAccountCb(selectedAccountId, function(ok)
    if not ok then
      Print('Failed to change account')
      return
    end
    Account_GetAccountCb(function(name)
      Print('Now current account: ' .. tostring(name))
    end)
  end)
end
