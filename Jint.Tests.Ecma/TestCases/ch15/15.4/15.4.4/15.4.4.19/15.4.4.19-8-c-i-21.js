/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-8-c-i-21.js
 * @description Array.prototype.map - element to be retrieved is inherited accessor property without a get function on an Array-like object
 */


function testcase() {

        function callbackfn(val, idx, obj) {
            if (idx === 0) {
                return typeof val === "undefined";
            }
            return false;
        }

        var proto = { length: 2 };
        Object.defineProperty(proto, "0", {
            set: function () { },
            configurable: true
        });

        var Con = function () { };
        Con.prototype = proto;

        var child = new Con();

        var testResult = Array.prototype.map.call(child, callbackfn);

        return testResult[0] === true;
    }
runTestCase(testcase);
