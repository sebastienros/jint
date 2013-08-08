/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.3/15.4.3.2/15.4.3.2-1-2.js
 * @description Array.isArray applied to Boolean Object
 */


function testcase() {

        return !Array.isArray(new Boolean(false));
    }
runTestCase(testcase);
