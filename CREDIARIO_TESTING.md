# 🧪 Crediário Feature — Testing Checklist

**Status:** Full backend + frontend implementation complete  
**Date:** 2026-05-07  
**Feature:** 30-day credit/installment payment system

---

## Phase 1: Unit Tests (Backend)

### Run Tests
```bash
cd tests/unit/CardGameStore.Tests
dotnet test --filter "CreditarioServiceTests" -v normal
```

**Expected: 18/18 tests PASS ✅**

Tests cover:
- ✅ CreateAsync: Create valid credit, prevent duplicates, validate comanda, set 30-day expiration
- ✅ GetByUserAsync: Returns all user credits, empty list when none
- ✅ GetAbertoAsync: Returns only open credits
- ✅ GetVencidosAsync: Returns only overdue credits  
- ✅ GetAllAsync: Returns all credits (open + paid)
- ✅ MarkAsPaidAsync: Update status, prevent double payment, validate exists
- ✅ GetByIdAsync: Return credit, return null for non-existent
- ✅ HasOpenAsync: Check if user has open credit
- ✅ GetOpenAsync: Return open credit or null
- ✅ GetTotalDevidoAsync: Calculate sum, ignore paid, handle multiple

---

## Phase 2: API Integration (Backend)

### Start Backend
```bash
dotnet run
```

### Check Swagger Documentation
Visit: **http://localhost:5000/**

Verify these 7 endpoints exist:

1. **GET** `/api/crediarios`  
   - Lists ALL credits (admin only)
   - Response: `List<CrediariosDto>`

2. **GET** `/api/crediarios/abertos`  
   - Lists OPEN credits only (admin only)
   - Response: `List<CrediariosDto>`

3. **GET** `/api/crediarios/vencidos`  
   - Lists OVERDUE credits only (admin only)
   - Response: `List<CrediariosDto>`

4. **GET** `/api/crediarios/user/{userId}`  
   - Get user's credits (admin or self)
   - Response: `List<CrediariosDto>`
   - Auth: Check 403 if accessing other user (non-admin)

5. **GET** `/api/crediarios/user/{userId}/total`  
   - Get total amount owed
   - Response: `{ TotalDevidoEmReais: number }`

6. **GET** `/api/crediarios/{id}`  
   - Get specific credit (admin or owner)
   - Response: `CrediariosDto`

7. **PUT** `/api/crediarios/{id}/pagar`  
   - Mark as paid (admin only)
   - Body: `{ observacao?: string }`
   - Response: `CrediariosDto`

---

## Phase 3: End-to-End Integration Test

### Prerequisite: Create Test User & Admin
Use your existing admin account for testing.

### Test Flow: Create Credit via Comanda Closure

**Step 1: Create a Comanda**
1. Login as customer or use quick-login
2. Open a new comanda (or use existing)
3. Add items to comanda
4. Note the comanda ID

**Step 2: Close Comanda with Crediário Payment**
1. Go to **Admin Dashboard** → **Comandas**
2. Find the open comanda
3. Click "Fechar Comanda" / "Close Comanda"
4. Select payment method: **"Crediário (30 dias)"**
5. Submit

**Expected:**
- ✅ Comanda status changes to "Fechada"
- ✅ No error toast (success notification expected)
- ✅ Email notification sent to customer (check customer's email)

**Step 3: Verify Credit Created**
1. Go to **Admin Dashboard** → **Crediário**
2. Filter: **"Em Aberto"** (Open)
3. Should see the credit you just created

**Verify Details:**
- ✅ Customer name appears
- ✅ Amount matches comanda total
- ✅ Status: "Em Aberto"
- ✅ Days remaining: ~30 days
- ✅ No "Vencido" badge (red)

---

## Phase 4: Admin Dashboard Testing

### Navigate to `/admin/crediario`

**Test: Filter by "Em Aberto"**
- ✅ Shows only open credits
- ✅ Shows count in KPI
- ✅ Shows total value in reais

**Test: Filter by "Quitados"** (Paid)
- ✅ Shows only paid credits
- ✅ Shows count in KPI

**Test: Filter by "Todos"** (All)
- ✅ Shows all credits (open + paid)
- ✅ Shows count in KPI

**Test: KPI Metrics**
- ✅ "Em Aberto" count correct
- ✅ "Vencidos" count correct (0 for new credits)
- ✅ "Valor em Aberto" sums correctly
- ✅ "Quitados" count correct

**Test: Credit Card Display**
Each credit card should show:
- ✅ Customer name
- ✅ Customer email
- ✅ Status badge (green/red/orange)
- ✅ Amount in reais
- ✅ Date opened
- ✅ Days remaining or overdue info
- ✅ "Marcar Pago" button (if not paid)

---

## Phase 5: Mark as Paid Testing

### Test: Mark Credit as Paid

**Step 1: Find open credit**
1. Filter to "Em Aberto"
2. Click any open credit's "Marcar Pago" button

**Step 2: Confirmation**
- ✅ Confirmation dialog appears
- ✅ Shows customer name + amount
- ✅ Ask "Confirmar pagamento de R$ XXX.XX?"

**Step 3: Submit**
1. Click "Confirmar" / "Confirm"
2. Wait for async operation (spinning icon)

**Expected:**
- ✅ Success toast: "Crediário de [nome] quitado!"
- ✅ Button changes to disabled or disappears
- ✅ Status badge changes to green "Quitado"
- ✅ KPI updates (open count decreases, paid count increases)

**Step 4: Verify in Different Filters**
1. Filter to "Quitados" → Credit appears
2. Filter to "Todos" → Credit appears with green badge
3. Filter to "Em Aberto" → Credit no longer appears

---

## Phase 6: Users Page Badge Testing

### Navigate to `/admin/usuarios`

**Test: Credit Badge Display**

For each user with open credit:
- ✅ Badge shows: "R$ XXX.XX (Xd)" or "R$ XXX.XX (Vencido)"
- ✅ Badge color: **Orange/Amber** for open, **Red** for overdue
- ✅ Badge links to `/admin/crediario` when clicked
- ✅ User with no open credit: NO badge shown

**Test: Badge Accuracy**
1. Create another credit
2. Check usuarios page
3. Badge amount should match total owed
4. Days remaining should be accurate (±1 day)

---

## Phase 7: Authorization Testing

### Test: Non-Admin Access Restrictions

**Scenario 1: Customer tries to access `/admin/crediario`**
- ✅ Should redirect to login or show "Unauthorized"

**Scenario 2: Customer accesses `/api/crediarios` via API**
- ✅ Returns 401 or 403 Forbidden

**Scenario 3: Customer accesses their own credit** `/api/crediarios/user/{theirId}`
- ✅ Should return their credits (allowed)

**Scenario 4: Customer accesses other user's credits** `/api/crediarios/user/{otherId}`
- ✅ Returns 403 Forbidden (unless admin)

**Scenario 5: Customer tries to mark credit as paid** `/api/crediarios/{id}/pagar`
- ✅ Returns 403 Forbidden

---

## Phase 8: Edge Cases

### Test: Multiple Open Credits per User
1. Close 2 comandas for same customer with Crediário payment
2. Expected: **SHOULD FAIL** — system blocks second open credit
3. ✅ Error message: "Este cliente já possui um crediário em aberto..."

### Test: Overdue Credit
1. Create a credit manually in DB (set DataVencimento to yesterday)
   ```sql
   -- Or wait 30 days in production :)
   UPDATE "Crediarios" SET "DataVencimento" = NOW() - INTERVAL '1 day' WHERE ...
   ```
2. Go to `/admin/crediario`
3. Filter "Em Aberto"
4. ✅ Credit shows with **RED "Vencido"** badge
5. Days remaining shows negative: "Vencido há X dias"

### Test: Zero Amount Credit
- System should handle R$ 0.00 (unlikely but safe to test)

---

## Phase 9: Data Persistence

### Test: Refresh Page
1. Go to `/admin/crediario`
2. Mark a credit as paid
3. **Refresh page** (F5 or Cmd+R)
4. ✅ Credit still shows as paid (persisted to DB)

### Test: Log Out & Back In
1. Mark credit as paid
2. Logout from admin
3. Login as admin again
4. Go to `/admin/crediario`
5. ✅ Credit still shows as paid

---

## Phase 10: Performance

### Test: Load Time
1. Create 50+ credits
2. Go to `/admin/crediario`
3. ✅ Page loads in < 2 seconds
4. ✅ No lag when filtering

### Test: KPI Calculation
1. With many credits, KPI metrics calculate in < 1 second

---

## Deployment Checklist

Before merging to main:

- [ ] All 18 unit tests pass
- [ ] All 7 API endpoints respond correctly
- [ ] Frontend pages render without console errors
- [ ] Authorization checks work
- [ ] Email notification sent on credit creation
- [ ] Badge shows correctly on usuarios page
- [ ] Mark as paid works and persists
- [ ] No TypeScript compilation errors
- [ ] No ESLint warnings in frontend code

---

## If Tests Fail

### Backend Tests Fail
1. Check test output for specific assertion failure
2. Read error message carefully
3. Run single test: `dotnet test --filter "TestNameHere"`
4. Check InMemory DB seeding in SeedAsync()

### API Not Responding
1. Check backend is running: `dotnet run`
2. Check port 5000 is accessible
3. Check JWT token is valid in Swagger UI
4. Check firewall isn't blocking

### Frontend Not Working
1. Check console for JavaScript errors (F12)
2. Check Network tab for failed API calls
3. Verify backend is running
4. Clear browser cache (Ctrl+Shift+Del)
5. Restart dev server: `npm run dev`

### Badge Not Showing
1. Verify open credit exists in DB
2. Check API call returns data: open browser DevTools → Network tab
3. Check userId matches
4. Verify status === 'Aberto' (case-sensitive)

---

## Quick Test Commands

```bash
# Backend: Run all tests
cd tests/unit/CardGameStore.Tests
dotnet test -v normal

# Backend: Run only Crediário tests
dotnet test --filter "CreditarioServiceTests" -v normal

# Backend: Start API
dotnet run

# Frontend: Start dev server
cd frontend
npm run dev

# Frontend: Build for production
npm run build
```

---

## Success Criteria ✅

**Backend:**
- [ ] 18/18 unit tests pass
- [ ] All 7 API endpoints working
- [ ] Proper authorization (401/403 responses)
- [ ] Credits persist to database

**Frontend:**
- [ ] Dashboard loads and displays credits
- [ ] Filters work correctly (Aberto/Pago/Todos)
- [ ] Mark as paid works with confirmation
- [ ] Badge shows on usuarios page with correct amount
- [ ] No console errors or warnings

**Integration:**
- [ ] Can create credit via comanda closure
- [ ] Email sent to customer
- [ ] Credit appears in dashboard
- [ ] Admin can mark as paid
- [ ] All data persists after page refresh

---

**Questions during testing?**  
Check the implementation details in `CREDIARIO_IMPLEMENTATION.md` in memory files.

**Ready to ship!** 🚀
