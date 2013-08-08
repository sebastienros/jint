/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * Refer 11.1.5; 
 *   The production
 *      PropertyNameAndValueList :  PropertyNameAndValueList , PropertyAssignment
 *    4. If previous is not undefined then throw a SyntaxError exception if any of the following conditions are true
 *      b.IsDataDescriptor(previous) is true and IsAccessorDescriptor(propId.descriptor) is true.
 *
 * @path ch11/11.1/11.1.5/11.1.5_4-4-b-1.js
 * @description Object literal - SyntaxError if a data property definition is followed by get accessor definition with the same name
 */


function testcase() {
  try
  {
    eval("({foo : 1, get foo(){}});");
    return false;
  }
  catch(e)
  {
    return e instanceof SyntaxError;
  }
 }
runTestCase(testcase);
