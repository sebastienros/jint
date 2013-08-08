/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.12/15.2.3.12-4-1.js
 * @description Object.isFrozen returns false if extensible is true
 */


function testcase() {
        return !Object.isFrozen({});
    }
runTestCase(testcase);
