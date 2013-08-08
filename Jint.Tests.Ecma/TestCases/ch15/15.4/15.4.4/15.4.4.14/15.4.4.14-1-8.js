/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-1-8.js
 * @description Array.prototype.indexOf applied to String object
 */


function testcase() {

        var obj = new String("null");

        return Array.prototype.indexOf.call(obj, 'l') === 2;
    }
runTestCase(testcase);
