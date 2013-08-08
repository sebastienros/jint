/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-2-19.js
 * @description Array.prototype.lastIndexOf applied to String object which implements its own property get method
 */


function testcase() {

        var obj = function (a, b) {
            return a + b;
        };
        obj[1] = "b";
        obj[2] = "c";

        return Array.prototype.lastIndexOf.call(obj, obj[1]) === 1 &&
            Array.prototype.lastIndexOf.call(obj, obj[2]) === -1;
    }
runTestCase(testcase);
