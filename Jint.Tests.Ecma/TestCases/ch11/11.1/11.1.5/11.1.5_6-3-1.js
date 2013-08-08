/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * Refer 11.1.5; 
 *   The production
 *      PropertyAssignment : get PropertyName ( ) { FunctionBody } 
 *   3.Let desc be the Property Descriptor{[[Get]]: closure, [[Enumerable]]: true, [[Configurable]]: true}
 *
 * @path ch11/11.1/11.1.5/11.1.5_6-3-1.js
 * @description Object literal - property descriptor for get property assignment
 */


function testcase() {

  eval("var o = {get foo(){return 1;}};");
  var desc = Object.getOwnPropertyDescriptor(o,"foo");
  if(desc.enumerable === true &&
     desc.configurable === true)
    return true;
 }
runTestCase(testcase);
