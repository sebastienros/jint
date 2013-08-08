/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-9-c-i-15.js
 * @description Array.prototype.reduce - element to be retrieved is inherited accessor property on an Array-like object
 */


function testcase() {

        var testResult = false;
        var initialValue = 0;
        function callbackfn(prevVal, curVal, idx, obj) {
            if (idx === 1) {
                testResult = (curVal === 1);
            }
        }

        var proto = { 0: 0, 2: 2 };

        Object.defineProperty(proto, "1", {
            get: function () {
                return 1;
            },
            configurable: true
        });

        var Con = function () { };
        Con.prototype = proto;

        var child = new Con();
        child.length = 3;

        Array.prototype.reduce.call(child, callbackfn, initialValue);
        return testResult;
    }
runTestCase(testcase);
