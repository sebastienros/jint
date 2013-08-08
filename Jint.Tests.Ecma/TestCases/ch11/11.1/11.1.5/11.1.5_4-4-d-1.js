/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * Refer 11.1.5; 
 *   The production
 *     PropertyNameAndValueList :  PropertyNameAndValueList , PropertyAssignment
 *   4. If previous is not undefined then throw a SyntaxError exception if any of the following conditions are true
 *     d.	IsAccessorDescriptor(previous) is true and IsAccessorDescriptor(propId.descriptor) is true and either both previous and propId.descriptor have [[Get]] fields or both previous and propId.descriptor have [[Set]] fields
 *
 * @path ch11/11.1/11.1.5/11.1.5_4-4-d-1.js
 * @description Object literal - SyntaxError for duplicate property name (get,get)
 */


function testcase() {
  try
  {
    eval("({get foo(){}, get foo(){}});");
    return false;
  }
  catch(e)
  {
    return e instanceof SyntaxError;
  }
 }
runTestCase(testcase);
