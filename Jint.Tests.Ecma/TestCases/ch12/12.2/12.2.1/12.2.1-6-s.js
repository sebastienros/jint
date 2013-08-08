/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch12/12.2/12.2.1/12.2.1-6-s.js
 * @description eval - a Function assigning into 'eval' will not throw any error if contained within strict mode and its body does not start with strict mode
 * @onlyStrict
 */


function testcase() {
  'use strict';
  
    var f = Function('eval = 42;');
    f();
    return true;
 }
runTestCase(testcase);
