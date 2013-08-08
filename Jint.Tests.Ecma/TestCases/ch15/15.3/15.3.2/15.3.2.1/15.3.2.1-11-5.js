/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.3/15.3.2/15.3.2.1/15.3.2.1-11-5.js
 * @description Duplicate combined parameter name in Function constructor allowed if body is not strict
 */


function testcase()
{   
    Function('a,a','return;');
    return true;
 }
runTestCase(testcase);
