/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch12/12.2/12.2.1/12.2.1-17-s.js
 * @description A Function constructor (called as a function) assigning into 'arguments' will not throw any error if contained within strict mode and its body does not start with strict mode
 * @onlyStrict
 */




function testcase() {
  'use strict';
  
    var f = Function('arguments = 42;');
    f();
    return true;
}
runTestCase(testcase);