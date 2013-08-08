/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-1-4.js
 * @description Array.prototype.indexOf applied to Boolean Object
 */


function testcase() {

        var obj = new Boolean(false);
        obj.length = 2;
        obj[1] = true;

        return Array.prototype.indexOf.call(obj, true) === 1;
    }
runTestCase(testcase);
