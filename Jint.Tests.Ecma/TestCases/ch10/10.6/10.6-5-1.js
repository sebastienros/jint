/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch10/10.6/10.6-5-1.js
 * @description [[Prototype]] property of Arguments is set to Object prototype object
 */


function testcase() {
  if(Object.getPrototypeOf(arguments) === Object.getPrototypeOf({}))
    return true;
 }
runTestCase(testcase);
