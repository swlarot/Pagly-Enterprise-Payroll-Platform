#!/bin/bash
# Audit script to check for security violations
# Run weekly: bash scripts/security/audit.sh

echo "🔍 Pagly Security Audit"
echo "======================="

VIOLATIONS=0

# 1. Check for secrets in staged/committed files
echo -e "\n1️⃣  Checking for committed secrets..."
if command -v git-secrets &> /dev/null; then
    git secrets --scan
else
    echo "⚠️  git-secrets not installed"
fi

# 2. Find overly permissive API endpoints
echo -e "\n2️⃣  Checking for missing authorization..."
grep -r "GET\|POST\|PUT\|DELETE" src/ --include="*.cs" \
    | grep -v "\[Authorize\]\|\[AllowAnonymous\]" \
    | head -20 && echo "⚠️  Found endpoints without explicit auth" && VIOLATIONS=$((VIOLATIONS + 1))

# 3. Check for hardcoded connection strings
echo -e "\n3️⃣  Checking for hardcoded connection strings..."
grep -r "Server=\|Password=" src/ --include="*.cs" --include="*.json" \
    | grep -v "appsettings.example\|.gitignore" \
    && echo "❌ HARDCODED CONNECTION STRING FOUND" && VIOLATIONS=$((VIOLATIONS + 1))

# 4. Verify encryption on sensitive fields
echo -e "\n4️⃣  Checking for encrypted sensitive fields..."
SENSITIVE_FIELDS=$(grep -r "SSN\|BankAccount\|CreditCard" src/Core/Pagly.Domain --include="*.cs")
ENCRYPTED_FIELDS=$(echo "$SENSITIVE_FIELDS" | grep "\[Encrypted\]")

if [ $(echo "$SENSITIVE_FIELDS" | wc -l) -gt $(echo "$ENCRYPTED_FIELDS" | wc -l) ]; then
    echo "⚠️  Some sensitive fields may not be encrypted"
    VIOLATIONS=$((VIOLATIONS + 1))
fi

# 5. Check API key validation
echo -e "\n5️⃣  Checking API key validation logic..."
if grep -r "ValidateApiKey\|CheckApiKey" src/ --include="*.cs" | grep -q "."; then
    echo "✅ API key validation found"
else
    echo "⚠️  API key validation may be missing"
fi

# 6. Check rate limiting
echo -e "\n6️⃣  Checking rate limiting configuration..."
if grep -r "RateLimit\|FixedWindowRateLimiter" src/ --include="*.cs" | grep -q "."; then
    echo "✅ Rate limiting configured"
else
    echo "⚠️  Rate limiting not found"
    VIOLATIONS=$((VIOLATIONS + 1))
fi

# 7. Check audit logging
echo -e "\n7️⃣  Checking audit logging..."
if grep -r "AuditLog\|LogAsync" src/ --include="*.cs" | grep -q "."; then
    echo "✅ Audit logging found"
else
    echo "⚠️  Audit logging not configured"
    VIOLATIONS=$((VIOLATIONS + 1))
fi

# 8. Check for SQL injection prevention
echo -e "\n8️⃣  Checking for parameterized queries..."
if grep -r "ExecuteSqlAsync\|ExecuteSqlRawAsync" src/ --include="*.cs" | grep -q "."; then
    echo "⚠️  Raw SQL execution found - verify parameterization"
fi

# Summary
echo -e "\n======================================="
if [ $VIOLATIONS -eq 0 ]; then
    echo "✅ Security audit passed!"
else
    echo "❌ Found $VIOLATIONS security issues"
    echo "Review above and fix before production push"
fi
echo "======================================="

exit $VIOLATIONS
