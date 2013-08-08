/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-9-c-i-15.js
 * @description Array.prototype.filter - element to be retrieved is inherited accessor property on an Array-like object
 */


function testcase() {

        function callbackfn(val, idx, obj) {
            return val === 11 && idx === 1;
        }

        var proto = {};

        Object.defineProperty(proto, "1", {
            get: function () {
                return 11;
            },
            configurable: true
        });

        var Con = function () { };
        Con.prototype = proto;

        var child = new Con();
        child.length = 20;
        var newArr = Array.prototype.filter.call(child, callbackfn);

        return newArr.length === 1 && newArr[0] === 11;
    }
runTestCase(testcase);
