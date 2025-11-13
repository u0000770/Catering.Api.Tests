using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DTO;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ThAmCo.Catering.Model;
using Catering.Api.Controllers;
using Xunit;

namespace Catering.Api.Tests.Controllers
{
    public class FoodBookingsControllerTests
    {
        private CateringContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<CateringContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            var context = new CateringContext(options);

            context.Database.EnsureCreated();

            // Clean all relevant tables (in case Seed() added data)
            context.FoodBookings.RemoveRange(context.FoodBookings);
            context.MenuFoodItems.RemoveRange(context.MenuFoodItems);
            context.FoodItems.RemoveRange(context.FoodItems);
            context.Menus.RemoveRange(context.Menus);
            context.SaveChanges();

            return context;
        }

        private FoodBookingsController CreateController(CateringContext context)
        {
            return new FoodBookingsController(context);
        }

        #region GET /api/FoodBookings

        [Fact]
        public async Task GetFoodBookings_ReturnsAllBookings()
        {
            // Arrange
            using var context = CreateContext();

            context.FoodBookings.AddRange(
                new FoodBooking
                {
                    FoodBookingId = 1,
                    ClientReferenceId = 100,
                    MenuId = 10,
                    NumberOfGuests = 2
                },
                new FoodBooking
                {
                    FoodBookingId = 2,
                    ClientReferenceId = 101,
                    MenuId = 11,
                    NumberOfGuests = 4
                }
            );

            await context.SaveChangesAsync();

            var controller = CreateController(context);

            // Act
            var result = await controller.GetFoodBookings();

            // Assert
            // Method returns the list directly, not Ok(...)
            Assert.Null(result.Result);
            var bookings = Assert.IsAssignableFrom<IEnumerable<FoodBooking>>(result.Value);
            Assert.Equal(2, bookings.Count());
        }

        [Fact]
        public async Task GetFoodBooking_ExistingId_ReturnsBooking()
        {
            // Arrange
            using var context = CreateContext();

            var booking = new FoodBooking
            {
                FoodBookingId = 5,
                ClientReferenceId = 200,
                MenuId = 20,
                NumberOfGuests = 3
            };

            context.FoodBookings.Add(booking);
            await context.SaveChangesAsync();

            var controller = CreateController(context);

            // Act
            var result = await controller.GetFoodBooking(5);

            // Assert
            Assert.Null(result.Result); // no NotFound
            var returned = Assert.IsType<FoodBooking>(result.Value);
            Assert.Equal(5, returned.FoodBookingId);
            Assert.Equal(200, returned.ClientReferenceId);
            Assert.Equal(20, returned.MenuId);
            Assert.Equal(3, returned.NumberOfGuests);
        }

        [Fact]
        public async Task GetFoodBooking_NonExistingId_ReturnsNotFound()
        {
            // Arrange
            using var context = CreateContext();
            var controller = CreateController(context);

            // Act
            var result = await controller.GetFoodBooking(999);

            // Assert
            Assert.IsType<NotFoundResult>(result.Result);
        }

        #endregion

        #region POST /api/FoodBookings

        [Fact]
        public async Task PostFoodBooking_ValidRequest_CreatesBookingAndReturnsCreated()
        {
            // Arrange
            using var context = CreateContext();

            // Menu must exist for menuExists(...) to be true
            var menu = new Menu { MenuId = 10, MenuName = "Wedding Menu" };
            context.Menus.Add(menu);
            await context.SaveChangesAsync();

            var controller = CreateController(context);

            var dto = new FoodBookingDto
            {
                clientReferenceId = 500,
                menuId = 10,
                numberOfGuests = 50
            };

            // Act
            var result = await controller.PostFoodBooking(dto);

            // Assert basic CreatedAtAction behaviour
            var created = Assert.IsType<CreatedAtActionResult>(result.Result);
            Assert.Equal("GetFoodBooking", created.ActionName);

            // We don't rely on RouteValues, because this overload doesn't set them.
            // Instead, we look up the created booking by its known properties.

            var bookingWithMenu = await context.FoodBookings
                .Include(b => b.Menu)
                .SingleOrDefaultAsync(b =>
                    b.ClientReferenceId == 500 &&
                    b.MenuId == 10 &&
                    b.NumberOfGuests == 50);

            Assert.NotNull(bookingWithMenu);
            Assert.Equal(500, bookingWithMenu!.ClientReferenceId);
            Assert.Equal(10, bookingWithMenu.MenuId);
            Assert.Equal(50, bookingWithMenu.NumberOfGuests);

            // Navigation property checks
            Assert.NotNull(bookingWithMenu.Menu);
            Assert.Equal(10, bookingWithMenu.Menu!.MenuId);
            Assert.Equal("Wedding Menu", bookingWithMenu.Menu.MenuName);
        }


        [Fact]
        public async Task PostFoodBooking_DuplicateBooking_ReturnsConflict()
        {
            // Arrange
            using var context = CreateContext();

            var menu = new Menu { MenuId = 20, MenuName = "Party Menu" };
            context.Menus.Add(menu);

            var existing = new FoodBooking
            {
                FoodBookingId = 1,
                ClientReferenceId = 600,
                MenuId = 20,
                NumberOfGuests = 10
            };
            context.FoodBookings.Add(existing);

            await context.SaveChangesAsync();

            var controller = CreateController(context);

            var dto = new FoodBookingDto
            {
                clientReferenceId = 600,
                menuId = 20,
                numberOfGuests = 12
            };

            // Act
            var result = await controller.PostFoodBooking(dto);

            // Assert
            var conflict = Assert.IsType<ConflictObjectResult>(result.Result);
            var payload = conflict.Value as dynamic;
            Assert.NotNull(payload);
            // Optional: assert on the message
            // Assert.Contains("existing booking", (string)payload.message);

            // Ensure only one booking for that client+menu
            Assert.Equal(1, context.FoodBookings.Count(fb => fb.ClientReferenceId == 600 && fb.MenuId == 20));
        }

        [Fact]
        public async Task PostFoodBooking_Invalid_NoMenuExists_ReturnsBadRequest()
        {
            // Arrange
            using var context = CreateContext();

            var controller = CreateController(context);

            var dto = new FoodBookingDto
            {
                clientReferenceId = 700,
                menuId = 999,             // no such menu
                numberOfGuests = 5
            };

            // Act
            var result = await controller.PostFoodBooking(dto);

            // Assert
            Assert.IsType<BadRequestResult>(result.Result);
        }

        [Fact]
        public async Task PostFoodBooking_Invalid_NullClientReference_ReturnsBadRequest()
        {
            // Arrange
            using var context = CreateContext();

            var menu = new Menu { MenuId = 30, MenuName = "Conference Menu" };
            context.Menus.Add(menu);
            await context.SaveChangesAsync();

            var controller = CreateController(context);

            var dto = new FoodBookingDto
            {
                clientReferenceId = null,  // invalid
                menuId = 30,
                numberOfGuests = 10
            };

            // Act
            var result = await controller.PostFoodBooking(dto);

            // Assert
            Assert.IsType<BadRequestResult>(result.Result);
        }

        [Fact]
        public async Task PostFoodBooking_Invalid_NumberOfGuestsZero_ReturnsBadRequest()
        {
            // Arrange
            using var context = CreateContext();

            var menu = new Menu { MenuId = 40, MenuName = "Test Menu" };
            context.Menus.Add(menu);
            await context.SaveChangesAsync();

            var controller = CreateController(context);

            var dto = new FoodBookingDto
            {
                clientReferenceId = 800,
                menuId = 40,
                numberOfGuests = 0        // invalid
            };

            // Act
            var result = await controller.PostFoodBooking(dto);

            // Assert
            Assert.IsType<BadRequestResult>(result.Result);
        }

        #endregion

        #region PUT /api/FoodBookings/{id}

        [Fact]
        public async Task PutFoodBooking_ValidUpdate_UpdatesBookingAndReturnsNoContent()
        {
            // Arrange
            using var context = CreateContext();

            // Old + new menu
            var oldMenu = new Menu { MenuId = 50, MenuName = "Old Menu" };
            var newMenu = new Menu { MenuId = 51, MenuName = "New Menu" };
            context.Menus.AddRange(oldMenu, newMenu);

            var booking = new FoodBooking
            {
                FoodBookingId = 10,
                ClientReferenceId = 900,
                MenuId = 50,
                NumberOfGuests = 5
            };
            context.FoodBookings.Add(booking);

            await context.SaveChangesAsync();

            var controller = CreateController(context);

            var dto = new FoodBookingDto
            {
                BookingId = 10,
                clientReferenceId = 900,  // must match existing booking
                menuId = 51,              // update to new menu
                numberOfGuests = 20
            };

            // Act
            var result = await controller.PutFoodBooking(10, dto);

            // Assert
            Assert.IsType<NoContentResult>(result);

            var updated = await context.FoodBookings.FindAsync(10);
            Assert.NotNull(updated);
            Assert.Equal(51, updated!.MenuId);
            Assert.Equal(20, updated.NumberOfGuests);
        }

        [Fact]
        public async Task PutFoodBooking_Invalid_IdMismatch_ReturnsBadRequest()
        {
            // Arrange
            using var context = CreateContext();

            var menu = new Menu { MenuId = 60, MenuName = "Menu A" };
            context.Menus.Add(menu);

            var booking = new FoodBooking
            {
                FoodBookingId = 11,
                ClientReferenceId = 901,
                MenuId = 60,
                NumberOfGuests = 5
            };
            context.FoodBookings.Add(booking);

            await context.SaveChangesAsync();

            var controller = CreateController(context);

            var dto = new FoodBookingDto
            {
                BookingId = 11,
                clientReferenceId = 901,
                menuId = 60,
                numberOfGuests = 10
            };

            // Act
            var result = await controller.PutFoodBooking(999, dto); // id mismatch

            // Assert
            Assert.IsType<BadRequestResult>(result);
        }

        [Fact]
        public async Task PutFoodBooking_Invalid_MenuDoesNotExist_ReturnsBadRequest()
        {
            // Arrange
            using var context = CreateContext();

            var existingMenu = new Menu { MenuId = 70, MenuName = "Existing Menu" };
            context.Menus.Add(existingMenu);

            var booking = new FoodBooking
            {
                FoodBookingId = 12,
                ClientReferenceId = 902,
                MenuId = 70,
                NumberOfGuests = 5
            };
            context.FoodBookings.Add(booking);
            await context.SaveChangesAsync();

            var controller = CreateController(context);

            var dto = new FoodBookingDto
            {
                BookingId = 12,
                clientReferenceId = 902,
                menuId = 999,            // non-existent menu
                numberOfGuests = 10
            };

            // Act
            var result = await controller.PutFoodBooking(12, dto);

            // Assert
            Assert.IsType<BadRequestResult>(result);
        }

        [Fact]
        public async Task PutFoodBooking_Invalid_NumberOfGuestsZero_ReturnsBadRequest()
        {
            // Arrange
            using var context = CreateContext();

            var menu = new Menu { MenuId = 80, MenuName = "Menu B" };
            context.Menus.Add(menu);

            var booking = new FoodBooking
            {
                FoodBookingId = 13,
                ClientReferenceId = 903,
                MenuId = 80,
                NumberOfGuests = 5
            };
            context.FoodBookings.Add(booking);
            await context.SaveChangesAsync();

            var controller = CreateController(context);

            var dto = new FoodBookingDto
            {
                BookingId = 13,
                clientReferenceId = 903,
                menuId = 80,
                numberOfGuests = 0       // invalid
            };

            // Act
            var result = await controller.PutFoodBooking(13, dto);

            // Assert
            Assert.IsType<BadRequestResult>(result);
        }

        [Fact]
        public async Task PutFoodBooking_Invalid_BookingDoesNotBelongToClient_ReturnsBadRequest()
        {
            // Arrange
            using var context = CreateContext();

            var menu = new Menu { MenuId = 90, MenuName = "Menu C" };
            context.Menus.Add(menu);

            var booking = new FoodBooking
            {
                FoodBookingId = 14,
                ClientReferenceId = 904,
                MenuId = 90,
                NumberOfGuests = 5
            };
            context.FoodBookings.Add(booking);
            await context.SaveChangesAsync();

            var controller = CreateController(context);

            var dto = new FoodBookingDto
            {
                BookingId = 14,
                clientReferenceId = 999,  // different client
                menuId = 90,
                numberOfGuests = 10
            };

            // Act
            var result = await controller.PutFoodBooking(14, dto);

            // Assert
            Assert.IsType<BadRequestResult>(result);
        }

        #endregion

        #region DELETE /api/FoodBookings/{id}

        [Fact]
        public async Task DeleteFoodBooking_ExistingId_RemovesAndReturnsNoContent()
        {
            // Arrange
            using var context = CreateContext();

            var booking = new FoodBooking
            {
                FoodBookingId = 15,
                ClientReferenceId = 905,
                MenuId = 100,
                NumberOfGuests = 10
            };
            context.FoodBookings.Add(booking);
            await context.SaveChangesAsync();

            var controller = CreateController(context);

            // Act
            var result = await controller.DeleteFoodBooking(15);

            // Assert
            Assert.IsType<NoContentResult>(result);
            Assert.False(context.FoodBookings.Any(fb => fb.FoodBookingId == 15));
        }

        [Fact]
        public async Task DeleteFoodBooking_NonExistingId_ReturnsNotFound()
        {
            // Arrange
            using var context = CreateContext();
            var controller = CreateController(context);

            // Act
            var result = await controller.DeleteFoodBooking(9999);

            // Assert
            Assert.IsType<NotFoundResult>(result);
        }

        #endregion
    }
}
