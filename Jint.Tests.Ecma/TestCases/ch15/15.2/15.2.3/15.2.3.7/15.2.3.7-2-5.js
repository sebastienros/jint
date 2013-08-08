/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-2-5.js
 * @description Object.defineProperties - argument 'Properties' is any interesting number
 */


function testcase() {

        var obj = { "123": 100 };
        var obj1 = Object.defineProperties(obj, -12);
        return obj === obj1;

    }
runTestCase(testcase);
