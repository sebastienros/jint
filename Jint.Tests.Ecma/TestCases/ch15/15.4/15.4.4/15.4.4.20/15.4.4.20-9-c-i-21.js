/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-9-c-i-21.js
 * @description Array.prototype.filter - element to be retrieved is inherited accessor property without a get function on an Array-like object
 */


function testcase() {

        function callbackfn(val, idx, obj) {
            return val === undefined && idx === 1;
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
        var newArr = Array.prototype.filter.call(child, callbackfn);

        return newArr.length === 1 && newArr[0] === undefined;
    }
runTestCase(testcase);
