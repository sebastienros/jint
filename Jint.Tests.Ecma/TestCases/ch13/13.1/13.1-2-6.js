/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch13/13.1/13.1-2-6.js
 * @description arguments allowed as formal parameter name of a non-strict function expression
 */


function testcase()
{
    eval("(function foo(arguments){});");
    return true;
 }
runTestCase(testcase);
