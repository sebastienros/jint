/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-1-8.js
 * @description Array.prototype.reduce applied to String object
 */


function testcase() {

        function callbackfn(prevVal, curVal, idx, obj) {
            return obj instanceof String;
        }

        var obj = new String("abc");

        return  Array.prototype.reduce.call(obj, callbackfn, 1);
    }
runTestCase(testcase);
