/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-8-c-i-5.js
 * @description Array.prototype.map - element to be retrieved is own data property that overrides an inherited accessor property on an Array-like object
 */


function testcase() {

        var kValue = "abc";

        function callbackfn(val, idx, obj) {
            if (idx === 5) {
                return val === kValue;
            }
            return false;
        }

        var proto = {};

        Object.defineProperty(proto, "5", {
            get: function () {
                return 11;
            },
            configurable: true
        });

        var Con = function () { };
        Con.prototype = proto;

        var child = new Con();
        child.length = 10;
        Object.defineProperty(child, "5", {
            value: kValue,
            configurable: true
        });

        var testResult = Array.prototype.map.call(child, callbackfn);

        return testResult[5] === true;
    }
runTestCase(testcase);
