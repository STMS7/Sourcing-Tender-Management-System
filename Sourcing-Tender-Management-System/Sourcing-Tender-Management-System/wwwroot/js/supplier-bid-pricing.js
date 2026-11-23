(() => {
    'use strict';
    // Integer arithmetic mirrors decimal rounding on the server without binary float drift.
    function decimal(value) {
        const match = /^(\d+)(?:\.(\d+))?$/.exec(value || '0');
        if (!match) throw new Error('Invalid amount');
        return { value: BigInt(match[1] + (match[2] || '')), scale: 10n ** BigInt((match[2] || '').length) };
    }
    const round = (numerator, denominator) => (numerator * 2n + denominator) / (2n * denominator);
    const money = cents => `${cents / 100n}.${(cents % 100n).toString().padStart(2, '0')}`;
    document.querySelectorAll('form[data-bid-pricing]').forEach(form => {
        function update() {
            let subtotal = 0n;
            let tax = 0n;
            let total = 0n;
            try {
                for (const input of form.querySelectorAll('[data-item-quantity]')) {
                    if (input.validity.badInput || (input.value && !input.validity.valid)) throw new Error('Invalid price');
                    const price = decimal(input.value);
                    const quantity = decimal(input.dataset.itemQuantity);
                    subtotal += round(price.value * quantity.value * 100n, price.scale * quantity.scale);
                }
                tax = round(subtotal * 16n, 100n);
                total = subtotal + tax;
                for (const name of ['FeesAmount', 'ShippingAmount', 'InstallationAmount', 'MaintenanceAmount']) {
                    const input = form.elements.namedItem(name);
                    if (!input) continue;
                    if (input.validity.badInput || (input.value && !input.validity.valid)) throw new Error('Invalid extra');
                    const amount = decimal(input.value);
                    total += round(amount.value * 100n, amount.scale);
                }
                form.querySelector('[data-pricing-subtotal]').textContent = money(subtotal);
                form.querySelector('[data-pricing-tax]').textContent = money(tax);
                form.querySelector('[data-pricing-total]').textContent = money(total);
            } catch {
                form.querySelectorAll('[data-pricing-subtotal], [data-pricing-tax], [data-pricing-total]')
                    .forEach(output => { output.textContent = '—'; });
            }
        }
        form.addEventListener('input', update);
        update();
    });
})();
