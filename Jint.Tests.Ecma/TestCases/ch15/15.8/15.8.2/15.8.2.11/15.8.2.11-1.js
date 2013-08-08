/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.8/15.8.2/15.8.2.11/15.8.2.11-1.js
 * @description Math.max({}) is NaN
 */





function testcase() {
    return isNaN(Math.max({}));
}
runTestCase(testcase);