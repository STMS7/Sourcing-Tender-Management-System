(() => {
    const output = document.getElementById("offer-tax");
    if (!output) return;
    const quantities = [...document.querySelectorAll(".offer-quantity")];
    const prices = [...document.querySelectorAll('input[name$=".UnitPrice"]')];
    const roundMoney = value => Math.round((value + Number.EPSILON) * 100) / 100;
    const update = () => {
        const subtotal = prices.reduce((sum, price, i) =>
            sum + roundMoney(Number(price.value || 0) * Number(quantities[i].value)), 0);
        output.value = roundMoney(subtotal * 0.16).toFixed(2);
    };
    prices.forEach(price => price.addEventListener("input", update));
    update();
})();
