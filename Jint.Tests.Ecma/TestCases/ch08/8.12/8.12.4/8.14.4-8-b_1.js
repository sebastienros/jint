/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch08/8.12/8.12.4/8.14.4-8-b_1.js
 * @description Non-writable property on a prototype written to.
 */

function testcase() {   
    function foo() {};
    Object.defineProperty(foo.prototype, "bar", {value: "unwritable"}); 
    
    var o = new foo(); 
    o.bar = "overridden"; 
    return o.hasOwnProperty("bar")===false && o.bar==="unwritable";
}
runTestCase(testcase);
