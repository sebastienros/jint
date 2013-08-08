/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-2-18.js
 * @description Array.prototype.reduce applied to String object, which implements its own property get method
 */


function testcase() {

        function callbackfn(prevVal, curVal, idx, obj) {
            return (obj.length === 3);
        }

        var str = new String("012");

        return Array.prototype.reduce.call(str, callbackfn, 1) === true;
    }
runTestCase(testcase);
