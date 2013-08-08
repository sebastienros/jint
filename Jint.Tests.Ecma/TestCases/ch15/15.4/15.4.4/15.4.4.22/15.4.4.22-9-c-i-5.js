/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-9-c-i-5.js
 * @description Array.prototype.reduceRight - element to be retrieved is own data property that overrides an inherited accessor property on an Array-like object
 */


function testcase() {

        var testResult = false;
        function callbackfn(prevVal, curVal, idx, obj) {
            if (idx === 0) {
                testResult = (curVal === "0");
            }
        }

        var proto = {};

        Object.defineProperty(proto, "0", {
            get: function () {
                return 10;
            },
            configurable: true
        });

        var Con = function () { };
        Con.prototype = proto;

        var child = new Con();
        child.length = 2;
        Object.defineProperty(child, "0", {
            value: "0",
            configurable: true
        });
        child[1] = "1";

        Array.prototype.reduceRight.call(child, callbackfn, "initialValue");
        return testResult;
    }
runTestCase(testcase);
