/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch12/12.14/12.14-2.js
 * @description catch doesn't change declaration scope - var initializer in catch with same name as catch parameter changes parameter
 */


function testcase() {
  function capturedFoo() {return foo};
  foo = "prior to throw";
  try {
    throw new Error();
  }
  catch (foo) {
    var foo = "initializer in catch";
    return capturedFoo() !== "initializer in catch";
  }
  
 }
runTestCase(testcase);
