/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch12/12.14/12.14-1.js
 * @description catch doesn't change declaration scope - var initializer in catch with same name as catch parameter changes parameter
 */


function testcase() {
  foo = "prior to throw";
  try {
    throw new Error();
  }
  catch (foo) {
    var foo = "initializer in catch";
  }
 return foo === "prior to throw";
  
 }
runTestCase(testcase);
