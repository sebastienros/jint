/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-7-c-i-21.js
 * @description Array.prototype.forEach - element to be retrieved is inherited accessor property without a get function on an Array-like object
 */


function testcase() {

        var testResult = false;

        function callbackfn(val, idx, obj) {
            if (idx === 1) {
                testResult = (typeof val === "undefined");
            }
        }

        var proto = {};
        Object.defineProperty(proto, "1", {
            set: function () { },
            configurable: true
        });

        var Con = function () { };
        Con.prototype = proto;

        var child = new Con();
        child.length = 2;

        Array.prototype.forEach.call(child, callbackfn);

        return testResult;
    }
runTestCase(testcase);
