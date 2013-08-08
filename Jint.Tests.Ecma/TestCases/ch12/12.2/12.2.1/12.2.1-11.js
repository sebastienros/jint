/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch12/12.2/12.2.1/12.2.1-11.js
 * @description arguments as var identifier in eval code is allowed
 */


function testcase() {
    eval("var arguments;");
    return true;
 }
runTestCase(testcase);
