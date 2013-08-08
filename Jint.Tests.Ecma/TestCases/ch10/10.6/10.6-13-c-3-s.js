/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch10/10.6/10.6-13-c-3-s.js
 * @description arguments.callee is non-configurable in strict mode
 * @onlyStrict
 */


function testcase() {
  
  'use strict';    
  var desc = Object.getOwnPropertyDescriptor(arguments,"callee");
  return (desc.configurable === false &&
     desc.enumerable === false &&
     desc.hasOwnProperty('value') == false &&
     desc.hasOwnProperty('writable') == false &&
     desc.hasOwnProperty('get') == true &&
     desc.hasOwnProperty('set') == true);
 }
runTestCase(testcase);
