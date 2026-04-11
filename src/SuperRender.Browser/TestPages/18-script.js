// 18-script.js — External script for Test 18

// Test 18b: Modify existing element
var target = document.getElementById("js-target");
if (target) {
    target.textContent = "External JS loaded successfully! This text was set by 18-script.js.";
    target.style.backgroundColor = "#ddffdd";
    target.style.borderColor = "#33aa33";
    target.style.fontWeight = "bold";
}

// Test 18c: Create a new element styled by external CSS
var container = document.getElementById("dynamic-container");
if (container) {
    var item = document.createElement("div");
    item.className = "dynamic-item";
    item.textContent = "This element was created by 18-script.js and styled by 18-style.css (.dynamic-item class)";
    container.appendChild(item);
}
