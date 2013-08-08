/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-9-c-i-21.js
 * @description Array.prototype.reduceRight - element to be retrieved is inherited accessor property without a get function on an Array-like object
 */


function testcase() {
    
        var testResult = false;
        function callbackfn(prevVal, curVal, idx, obj) {
            if (idx === 1) {
                testResult = (typeof curVal === "undefined");
            }
        }

        var proto = { 0: 0, 2: 2 };

        Object.defineProperty(proto, "1", {
            set: function () { },
            configurable: true
        });

        var Con = function () { };
        Con.prototype = proto;

        var child = new Con();
        child.length = 3;

        Array.prototype.reduceRight.call(child, callbackfn, "initialValue");
        return testResult;


    }
runTestCase(testcase);
