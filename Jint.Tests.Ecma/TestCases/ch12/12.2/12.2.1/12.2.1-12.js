/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch12/12.2/12.2.1/12.2.1-12.js
 * @description arguments as local var identifier is allowed
 */


function testcase() {
    eval("(function (){var arguments;})");
    return true;
 }
runTestCase(testcase);
