/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch10/10.6/10.6-13-c-2-s.js
 * @description arguments.callee is exists in strict mode
 * @onlyStrict
 */


function testcase() {
  
  'use strict';    
  var desc = Object.getOwnPropertyDescriptor(arguments,"callee");
  return desc !== undefined;
 }
runTestCase(testcase);
