/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * Refer 11.1.5; 
 *   The production
 *      PropertyAssignment : get PropertyName ( ) { FunctionBody } 
 *   3.Let desc be the Property Descriptor{[[Get]]: closure, [[Enumerable]]: true, [[Configurable]]: true}
 *
 * @path ch11/11.1/11.1.5/11.1.5_7-3-2.js
 * @description Object literal - property descriptor for set property assignment should not create a get function
 */


function testcase() {

  eval("var o = {set foo(arg){}};");
  var desc = Object.getOwnPropertyDescriptor(o,"foo");
  return desc.get === undefined
 }
runTestCase(testcase);
