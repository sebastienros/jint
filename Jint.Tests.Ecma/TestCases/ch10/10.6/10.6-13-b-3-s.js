/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch10/10.6/10.6-13-b-3-s.js
 * @description arguments.caller is non-configurable in strict mode
 * @onlyStrict
 */


function testcase() {
   
  'use strict';    
  var desc = Object.getOwnPropertyDescriptor(arguments,"caller");
  
  return (desc.configurable === false && 
     desc.enumerable === false && 
     desc.hasOwnProperty('value') == false  && 
     desc.hasOwnProperty('writable') == false &&
     desc.hasOwnProperty('get') == true && 
     desc.hasOwnProperty('set') == true);                                     
    
 }
runTestCase(testcase);
