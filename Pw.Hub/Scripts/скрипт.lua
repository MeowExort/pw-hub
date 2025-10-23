local n = 1
Print(tostring(n))

n = 2
Print(tostring(n))

Account_GetAccountsCb(function(accounts)

    n = 3
    Print(tostring(n))
    
end)

n = 4
Print(tostring(n))