/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch10/10.6/10.6-13-b-2-s.js
 * @description arguments.caller exists in strict mode
 * @onlyStrict
 */


function testcase() {
  
  'use strict';    
  var desc = Object.getOwnPropertyDescriptor(arguments,"caller");
  return desc!== undefined;
 }
runTestCase(testcase);
