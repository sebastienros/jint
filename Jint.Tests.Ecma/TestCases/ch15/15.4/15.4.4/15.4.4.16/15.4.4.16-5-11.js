/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-5-11.js
 * @description Array.prototype.every - String Object can be used as thisArg
 */


function testcase() {

        var accessed = false;
        var objString = new String();

        function callbackfn(val, idx, obj) {
            accessed = true;
            return this === objString;
        }

        

        return [11].every(callbackfn, objString) && accessed;
    }
runTestCase(testcase);
