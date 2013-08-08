/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.8/15.8.2/15.8.2.12/15.8.2.12-1.js
 * @description Math.min({}) is NaN
 */





function testcase() {
    return isNaN(Math.min({}));
}
runTestCase(testcase);